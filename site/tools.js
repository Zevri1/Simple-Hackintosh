(() => {
"use strict";
const $ = id => document.getElementById(id);
const toast = message => {
  const node = $("toast");
  node.textContent = message;
  node.classList.add("show");
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => node.classList.remove("show"), 2600);
};
const normalizePath = value => {
  const raw = String(value || "").replace(/\\/g, "/");
  if (raw.startsWith("/") || /^[a-z]:\//i.test(raw) || raw.startsWith("//")) {
    throw new Error("The package contains an absolute file path.");
  }
  const parts = raw.split("/").filter(part => part && part !== ".");
  if (parts.some(part => part === ".." || part.includes(":"))) {
    throw new Error("The package contains an unsafe file path.");
  }
  return parts.join("/");
};
let selectedEfi = null;

const toolTabs = Array.from(document.querySelectorAll("[data-tool-target]"));
const toolPanels = Array.from(document.querySelectorAll("[data-tool-panel]"));

function activateToolTab(button, focusButton) {
  const target = button.dataset.toolTarget;
  toolTabs.forEach(item => {
    const active = item === button;
    item.classList.toggle("active", active);
    item.setAttribute("aria-selected", String(active));
    item.tabIndex = active ? 0 : -1;
  });
  toolPanels.forEach(panel => {
    const active = panel.dataset.toolPanel === target;
    panel.hidden = !active;
    panel.classList.toggle("active", active);
    panel.setAttribute("aria-hidden", String(!active));
  });
  if (focusButton) button.focus();
}

toolTabs.forEach(button => {
  button.addEventListener("click", () => activateToolTab(button, false));
  button.addEventListener("keydown", event => {
    const index = toolTabs.indexOf(button);
    let nextIndex = index;
    if (event.key === "ArrowRight" || event.key === "ArrowDown") nextIndex = (index + 1) % toolTabs.length;
    else if (event.key === "ArrowLeft" || event.key === "ArrowUp") nextIndex = (index - 1 + toolTabs.length) % toolTabs.length;
    else if (event.key === "Home") nextIndex = 0;
    else if (event.key === "End") nextIndex = toolTabs.length - 1;
    else return;
    event.preventDefault();
    activateToolTab(toolTabs[nextIndex], true);
  });
});

const themeButton = $("tools-theme-toggle");
function updateToolsThemeButton() {
  const light = document.documentElement.dataset.theme === "light";
  themeButton.textContent = light ? "☾" : "☼";
  themeButton.setAttribute("aria-label", light ? "Switch to dark theme" : "Switch to light theme");
  themeButton.title = light ? "Switch to dark theme" : "Switch to light theme";
  themeButton.setAttribute("aria-pressed", String(!light));
}
themeButton.addEventListener("click", () => {
  const next = document.documentElement.dataset.theme === "light" ? "dark" : "light";
  document.documentElement.dataset.theme = next;
  try { localStorage.setItem("simple-hackintosh-theme", next); } catch (error) {}
  updateToolsThemeButton();
});
updateToolsThemeButton();

function addCheck(container, kind, title, detail) {
  const row = document.createElement("div");
  row.className = "check-item " + kind;
  const symbol = document.createElement("span");
  symbol.className = "check-symbol";
  symbol.textContent = kind === "ok" ? "✓" : kind === "warn" ? "!" : "×";
  const body = document.createElement("div");
  const strong = document.createElement("strong");
  strong.textContent = title;
  const p = document.createElement("p");
  p.textContent = detail;
  body.append(strong,p);
  row.append(symbol,body);
  container.appendChild(row);
}
function showChecks(container, title, checks, footnote) {
  container.replaceChildren();
  container.hidden = false;
  const failures = checks.filter(c => c.kind === "fail").length;
  const warnings = checks.filter(c => c.kind === "warn").length;
  const passed = checks.filter(c => c.kind === "ok").length;
  const head = document.createElement("div");
  head.className = "result-head";
  const h = document.createElement("h3");
  h.textContent = title;
  const summary = document.createElement("span");
  summary.className = "result-summary";
  summary.textContent = passed + " passed · " + warnings + " warnings · " + failures + " issues";
  const pill = document.createElement("span");
  pill.className = "score-pill" + (failures ? " bad" : warnings ? " warn" : "");
  pill.textContent = failures ? "ISSUES FOUND" : warnings ? "REVIEW NEEDED" : "STRUCTURE OK";
  head.append(h,summary,pill);
  const list = document.createElement("div");
  list.className = "check-list";
  checks.forEach(c => addCheck(list,c.kind,c.title,c.detail));
  const foot = document.createElement("p");
  foot.className = "result-foot";
  foot.textContent = footnote || "";
  container.append(head,list,foot);
}
function xmlNodeToValue(node) {
  const tag = node.tagName.toLowerCase();
  if (tag === "dict") {
    const result = {};
    const children = Array.from(node.children);
    for (let i=0;i<children.length;i++) {
      if (children[i].tagName.toLowerCase() !== "key") continue;
      const val = children[i+1];
      if (val) result[children[i].textContent] = xmlNodeToValue(val);
      i++;
    }
    return result;
  }
  if (tag === "array") return Array.from(node.children).map(xmlNodeToValue);
  if (tag === "true") return true;
  if (tag === "false") return false;
  if (tag === "integer" || tag === "real") return Number(node.textContent);
  return node.textContent.trim();
}
function parsePlistXml(xmlText) {
  if (xmlText.length > 5 * 1024 * 1024) throw new Error("The plist is larger than the 5 MB safety limit.");
  const doc = new DOMParser().parseFromString(xmlText, "application/xml");
  if (doc.querySelector("parsererror")) throw new Error("The file is not valid XML. Check that it is an XML plist, not a binary plist.");
  const root = doc.documentElement;
  if (!root || root.tagName.toLowerCase() !== "plist") throw new Error("The XML root element must be <plist>.");
  const value = Array.from(root.children).find(node => node.nodeType === 1);
  if (!value || value.tagName.toLowerCase() !== "dict") throw new Error("The top-level plist value must be a dictionary (<dict>).");
  return xmlNodeToValue(value);
}
function validateConfigObject(config, fileEntries) {
  const checks = [];
  const required = ["ACPI","Booter","DeviceProperties","Kernel","Misc","NVRAM","PlatformInfo","UEFI"];
  required.forEach(key => {
    const value = config[key];
    const valid = value && typeof value === "object" && !Array.isArray(value);
    checks.push({kind:valid?"ok":"fail",title:"Top-level section: " + key,detail:valid?"Section exists as a dictionary.":"Required OpenCore section is missing or is not a dictionary."});
  });
  const pairs = [
    ["ACPI","Add","array"],["Booter","Quirks","dict"],["DeviceProperties","Add","dict"],
    ["Kernel","Add","array"],["Kernel","Quirks","dict"],["Misc","Boot","dict"],
    ["Misc","Security","dict"],["NVRAM","Add","dict"],["PlatformInfo","Generic","dict"],
    ["UEFI","Drivers","array"],["UEFI","Output","dict"],["UEFI","Quirks","dict"]
  ];
  pairs.forEach(([section,key,type]) => {
    const parent = config[section];
    const value = parent && parent[key];
    const valid = type === "array" ? Array.isArray(value) : value && typeof value === "object" && !Array.isArray(value);
    checks.push({kind:valid?"ok":"warn",title:section + " → " + key,detail:valid?"Expected setting is present with a compatible container type.":"Setting is absent or has an unexpected type. Confirm it against the schema for your OpenCore version."});
  });
  if (config.Kernel && Array.isArray(config.Kernel.Add)) {
    const enabled = config.Kernel.Add.filter(item => item && item.Enabled === true).length;
    checks.push({kind:"ok",title:"Kernel → Add entries",detail:config.Kernel.Add.length + " entries found; " + enabled + " currently enabled."});
  }
  if (config.UEFI && Array.isArray(config.UEFI.Drivers)) {
    const drivers = config.UEFI.Drivers.filter(item => item && item.Enabled === true).map(item => item.Path).filter(Boolean);
    checks.push({kind:drivers.length?"ok":"warn",title:"Enabled UEFI drivers",detail:drivers.length ? drivers.join(", ") : "No enabled driver path was found."});
  }
  if (fileEntries) {
    const normalized = Array.from(fileEntries).map(path => normalizePath(path).toLowerCase());
    const exists = path => {
      const key = normalizePath(path).toLowerCase();
      return normalized.some(item => item === key || item.startsWith(key + "/"));
    };
    const refs = [];
    const acpi = config.ACPI && Array.isArray(config.ACPI.Add) ? config.ACPI.Add : [];
    acpi.filter(item=>item && item.Enabled && item.Path).forEach(item=>refs.push({type:"ACPI",path:"ACPI/"+item.Path,display:item.Path}));
    const kexts = config.Kernel && Array.isArray(config.Kernel.Add) ? config.Kernel.Add : [];
    kexts.filter(item=>item && item.Enabled && item.BundlePath).forEach(item=>refs.push({type:"Kext",path:"Kexts/"+item.BundlePath,display:item.BundlePath}));
    const drivers = config.UEFI && Array.isArray(config.UEFI.Drivers) ? config.UEFI.Drivers : [];
    drivers.filter(item=>item && item.Enabled && item.Path && item.Path.toLowerCase()!=="builtin").forEach(item=>refs.push({type:"Driver",path:"Drivers/"+item.Path,display:item.Path}));
    refs.forEach(ref => {
      const found = exists(ref.path);
      checks.push({kind:found?"ok":"fail",title:ref.type + " path: " + ref.display,detail:found?"Referenced item exists in the selected EFI package.":"Enabled entry points to a file or bundle not found in the selected package."});
    });
    if (!refs.length) checks.push({kind:"warn",title:"Path reference check",detail:"No enabled ACPI, Kext, or UEFI driver paths were available to compare."});
  }
  return checks;
}
function renderResultError(container,message) {
  showChecks(container,"Analysis could not be completed",[{kind:"fail",title:"Input error",detail:message}],"All processing is local to this browser tab. No file has been uploaded.");
}
function getEfiFilesFromFolder(files) {
  const map = new Map();
  for (const file of files) {
    const path = normalizePath(file.webkitRelativePath || file.name);
    if (path) map.set(path.toLowerCase(),{path,file});
  }
  return map;
}
async function readZip(file) {
  if (file.size > 150 * 1024 * 1024) throw new Error("ZIP is larger than the 150 MB safety limit.");
  const buffer = await file.arrayBuffer();
  const view = new DataView(buffer);
  const bytes = new Uint8Array(buffer);
  let eocd = -1;
  for (let i=Math.max(0,bytes.length-22-65535);i<=bytes.length-22;i++) {
    if (view.getUint32(i,true)===0x06054b50) eocd=i;
  }
  if (eocd<0) throw new Error("ZIP end-of-directory record was not found. The archive may be damaged or ZIP64.");
  const disk=view.getUint16(eocd+4,true),centralDisk=view.getUint16(eocd+6,true);
  const count=view.getUint16(eocd+10,true),centralSize=view.getUint32(eocd+12,true);
  let offset=view.getUint32(eocd+16,true),totalUncompressed=0;
  if(disk||centralDisk||count===0xffff||centralSize===0xffffffff||offset===0xffffffff) throw new Error("Multi-disk and ZIP64 archives are not supported. Extract the archive and choose its EFI folder instead.");
  if(count>12000) throw new Error("Archive contains too many entries.");
  const result=new Map();
  for(let i=0;i<count;i++){
    if(offset+46>bytes.length||view.getUint32(offset,true)!==0x02014b50) throw new Error("Invalid ZIP central directory entry.");
    const flags=view.getUint16(offset+8,true),method=view.getUint16(offset+10,true);
    const compressedSize=view.getUint32(offset+20,true),uncompressedSize=view.getUint32(offset+24,true);
    const nameLen=view.getUint16(offset+28,true),extraLen=view.getUint16(offset+30,true),commentLen=view.getUint16(offset+32,true);
    const localOffset=view.getUint32(offset+42,true);
    const nameBytes=bytes.slice(offset+46,offset+46+nameLen);
    const name=new TextDecoder((flags&0x800)?"utf-8":"windows-1252").decode(nameBytes);
    offset+=46+nameLen+extraLen+commentLen;
    if(!name||name.endsWith("/")) continue;
    const path=normalizePath(name);
    if(!path||path.split("/").includes("..")) throw new Error("The archive contains an unsafe path.");
    if(flags&1) throw new Error("Encrypted ZIP entries cannot be inspected. Use an unencrypted archive or select the extracted folder.");
    totalUncompressed+=uncompressedSize;
    if(uncompressedSize>70*1024*1024||totalUncompressed>300*1024*1024) throw new Error("The uncompressed archive exceeds the safety limit.");
    if(localOffset+30>bytes.length||view.getUint32(localOffset,true)!==0x04034b50) throw new Error("Invalid local ZIP file header.");
    const dataStart=localOffset+30+view.getUint16(localOffset+26,true)+view.getUint16(localOffset+28,true);
    const compressed=bytes.slice(dataStart,dataStart+compressedSize);
    let content;
    if(method===0) content=compressed;
    else if(method===8){
      if(typeof DecompressionStream==="undefined") throw new Error("This browser cannot decompress ZIP entries. Extract the archive and select the EFI folder instead.");
      try{
        const stream=new Blob([compressed]).stream().pipeThrough(new DecompressionStream("deflate-raw"));
        content=new Uint8Array(await new Response(stream).arrayBuffer());
      }catch(error){throw new Error("Could not decompress "+path+". Try extracting the ZIP and selecting the folder.");}
    }else throw new Error("Unsupported ZIP compression method "+method+" in "+path+".");
    if(content.length!==uncompressedSize) throw new Error("Size mismatch while reading "+path+".");
    result.set(path.toLowerCase(),{path,content});
  }
  return result;
}
function findMapPath(map,suffix){
  const key=normalizePath(suffix).toLowerCase();
  for(const [path,value] of map.entries()) if(path===key||path.endsWith("/"+key)) return value;
  return null;
}
function inspectEfiMap(map){
  const checks=[],paths=Array.from(map.values()).map(item=>item.path);
  const has=path=>map.has(normalizePath(path).toLowerCase());
  const configItem=findMapPath(map,"OC/config.plist");
  if(!configItem){
    checks.push({kind:"fail",title:"OpenCore config.plist",detail:"OC/config.plist was not found. Select the EFI folder itself or a ZIP containing EFI/OC/config.plist."});
    return checks;
  }
  const ocIndex=configItem.path.toLowerCase().lastIndexOf("oc/config.plist");
  const ocBase=configItem.path.slice(0,ocIndex)+"OC/";
  const bootBase=ocBase.slice(0,-3);
  const openCore=has(ocBase+"OpenCore.efi");
  const bootFile=has(bootBase+"BOOT/BOOTx64.efi")||has("EFI/BOOT/BOOTx64.efi")||has("BOOT/BOOTx64.efi");
  checks.push({kind:openCore?"ok":"fail",title:"OpenCore.efi",detail:openCore?"Found in the OC directory.":"OC/OpenCore.efi is missing."});
  checks.push({kind:bootFile?"ok":"warn",title:"UEFI fallback loader",detail:bootFile?"BOOT/BOOTx64.efi was found.":"BOOT/BOOTx64.efi was not found. Confirm the package includes a bootloader for x64 UEFI systems."});
  const runtime=has(ocBase+"Drivers/OpenRuntime.efi");
  checks.push({kind:runtime?"ok":"warn",title:"OpenRuntime.efi",detail:runtime?"Found in OC/Drivers.":"Not found in OC/Drivers. Check whether your selected OpenCore configuration requires it."});
  const drivers=paths.filter(path=>path.toLowerCase().startsWith((ocBase+"Drivers/").toLowerCase()));
  const kexts=paths.filter(path=>path.toLowerCase().endsWith(".kext/info.plist"));
  const acpis=paths.filter(path=>/\.aml$/i.test(path));
  checks.push({kind:drivers.length?"ok":"warn",title:"OC/Drivers",detail:drivers.length+" driver-related file(s) found."});
  checks.push({kind:kexts.length?"ok":"warn",title:"Kext bundles",detail:kexts.length+" Info.plist file(s) inside .kext bundles found."});
  checks.push({kind:acpis.length?"ok":"warn",title:"ACPI tables",detail:acpis.length+" AML file(s) found. A package may legitimately have none depending on its configuration."});
  try{
    const plist=parsePlistXml(new TextDecoder().decode(configItem.content));
    checks.push({kind:"ok",title:"config.plist XML",detail:"The file parses as an XML property list with a top-level dictionary."});
    const relative=paths.filter(path=>path.toLowerCase().startsWith(ocBase.toLowerCase())).map(path=>path.slice(ocBase.length));
    const refs=validateConfigObject(plist,relative).filter(c=>/ path: /.test(c.title));
    refs.forEach(c=>checks.push(c));
  }catch(error){checks.push({kind:"fail",title:"config.plist parsing",detail:error.message});}
  return checks;
}
$("efi-zip").addEventListener("change",event=>{
  const file=event.target.files&&event.target.files[0];
  selectedEfi=file?{type:"zip",file}:null;
  $("efi-selection").textContent=file?file.name+" · "+(file.size/1024/1024).toFixed(2)+" MB":"No EFI package selected";
  $("efi-run").disabled=!selectedEfi;
});
$("efi-folder").addEventListener("change",event=>{
  const files=Array.from(event.target.files||[]);
  selectedEfi=files.length?{type:"folder",files}:null;
  $("efi-selection").textContent=files.length?files.length+" files selected · "+(files[0].webkitRelativePath||files[0].name).split("/")[0]:"No EFI package selected";
  $("efi-run").disabled=!selectedEfi;
});
const drop=$("efi-dropzone");
["dragenter","dragover"].forEach(type=>drop.addEventListener(type,event=>{event.preventDefault();drop.classList.add("dragover");}));
["dragleave","drop"].forEach(type=>drop.addEventListener(type,event=>{event.preventDefault();drop.classList.remove("dragover");}));
drop.addEventListener("drop",event=>{
  const files=Array.from(event.dataTransfer.files||[]);
  const zip=files.find(file=>file.name.toLowerCase().endsWith(".zip"));
  if(zip){selectedEfi={type:"zip",file:zip};$("efi-selection").textContent=zip.name+" · "+(zip.size/1024/1024).toFixed(2)+" MB";$("efi-run").disabled=false;return;}
  if(files.length&&files.some(file=>file.webkitRelativePath)){selectedEfi={type:"folder",files};$("efi-selection").textContent=files.length+" folder files selected";$("efi-run").disabled=false;}
  else toast("Drop a ZIP file or choose an extracted folder.");
});
$("efi-run").addEventListener("click",async()=>{
  if(!selectedEfi)return;
  const button=$("efi-run");button.disabled=true;button.textContent="Analyzing…";
  try{
    const map=selectedEfi.type==="zip"?await readZip(selectedEfi.file):getEfiFilesFromFolder(selectedEfi.files);
    if(!map.size)throw new Error("No files were found in the selected package.");
    showChecks($("efi-results"),"EFI inspection",inspectEfiMap(map),"Structural inspection only. This does not test bootability, kernel compatibility, firmware settings, or whether every OpenCore quirk is correct.");
    $("efi-results").scrollIntoView({behavior:"smooth",block:"nearest"});
  }catch(error){renderResultError($("efi-results"),error.message);}
  finally{button.disabled=false;button.innerHTML='Analyze EFI <span>→</span>';}
});
$("efi-reset").addEventListener("click",()=>{
  selectedEfi=null;$("efi-zip").value="";$("efi-folder").value="";$("efi-selection").textContent="No EFI package selected";$("efi-run").disabled=true;$("efi-results").hidden=true;$("efi-results").replaceChildren();
});

$("config-file").addEventListener("change",async event=>{
  const file=event.target.files&&event.target.files[0];
  $("config-selection").textContent=file?file.name:"No file selected";
  if(!file)return;
  try{$("config-text").value=await file.text();}catch(error){toast("Could not read the selected file.");}
});
$("config-run").addEventListener("click",()=>{
  const text=$("config-text").value.trim();
  if(!text){renderResultError($("config-results"),"Choose a config.plist file or paste its XML content.");return;}
  try{
    const config=parsePlistXml(text);
    showChecks($("config-results"),"Configuration review",validateConfigObject(config,null),"Checks XML syntax, key sections, and basic container types. It is not the official OpenCore schema validator and cannot verify version-specific settings or bootability.");
    $("config-results").scrollIntoView({behavior:"smooth",block:"nearest"});
  }catch(error){renderResultError($("config-results"),error.message);}
});
$("config-reset").addEventListener("click",()=>{
  $("config-file").value="";$("config-selection").textContent="No file selected";$("config-text").value="";$("config-results").hidden=true;$("config-results").replaceChildren();
});

const vendorNames={"8086":"Intel","10de":"NVIDIA","1002":"AMD","1022":"AMD","14e4":"Broadcom","168c":"Qualcomm Atheros","14c3":"MediaTek","17cb":"Qualcomm","10ec":"Realtek","144d":"Samsung","15b7":"Western Digital / SanDisk","1b4b":"Marvell","1d0f":"Amazon"};
function makeHardwareReport(){
  const labels=[["CPU","hw-cpu"],["GPU","hw-gpu"],["System / motherboard","hw-board"],["Wi-Fi / Bluetooth","hw-wireless"],["Storage","hw-storage"],["Ethernet / audio","hw-other"]];
  const fields=labels.map(([name,id])=>({name,value:$(id).value.trim()}));
  const raw=$("hw-raw").value.trim(),ids=[];
  const regex=/(?:VEN_([0-9a-f]{4})&DEV_([0-9a-f]{4})|\[([0-9a-f]{4}):([0-9a-f]{4})\])/ig;
  let match;
  while((match=regex.exec(raw))!==null){
    const vendor=(match[1]||match[3]).toUpperCase(),device=(match[2]||match[4]).toUpperCase();
    if(!ids.some(item=>item.vendor===vendor&&item.device===device))ids.push({vendor,device,name:vendorNames[vendor.toLowerCase()]||"Unknown vendor"});
  }
  const lines=["# Simple Hackintosh — Hardware Report","","> Generated locally in your browser. Review the report before sharing it.","","## Component summary",...fields.map(item=>"- **"+item.name+":** "+(item.value||"Not provided")),"","## PCI vendor/device IDs"];
  if(ids.length)ids.forEach(item=>lines.push("- PCI VEN_"+item.vendor+"&DEV_"+item.device+" — "+item.name));
  else lines.push("- No PCI vendor/device IDs were detected in the pasted text.");
  if(raw)lines.push("","## Source report",String.fromCharCode(96,96,96)+"text",raw.slice(0,24000),String.fromCharCode(96,96,96));
  lines.push("","## Review notes","- PCI vendor names are simple ID lookups; they do not establish macOS compatibility.","- A device's exact subsystem ID, revision, firmware, and connection type may matter.","- This report is evidence for further research, not an EFI configuration or compatibility guarantee.","","Generated by Simple Hackintosh Tools Center.");
  return {text:lines.join("\n"),ids};
}
$("hardware-run").addEventListener("click",()=>{
  const {text,ids}=makeHardwareReport(),results=$("hardware-results");
  results.replaceChildren();results.hidden=false;
  const head=document.createElement("div");head.className="result-head";
  const title=document.createElement("h3");title.textContent="Hardware report ready";
  const count=document.createElement("span");count.className="score-pill";count.textContent=ids.length+" PCI ID"+(ids.length===1?"":"s")+" detected";
  head.append(title,count);
  const preview=document.createElement("pre");preview.className="report-preview";preview.textContent=text;
  const actions=document.createElement("div");actions.className="report-actions";
  const download=document.createElement("button");download.className="action-button primary";download.type="button";download.textContent="Download Markdown report";
  download.addEventListener("click",()=>{
    const blob=new Blob([text],{type:"text/markdown;charset=utf-8"}),url=URL.createObjectURL(blob),a=document.createElement("a");
    a.href=url;a.download="simple-hackintosh-hardware-report.md";document.body.appendChild(a);a.click();a.remove();window.setTimeout(()=>URL.revokeObjectURL(url),1000);toast("Hardware report downloaded.");
  });
  const copy=document.createElement("button");copy.className="download-button";copy.type="button";copy.textContent="Copy report";
  copy.addEventListener("click",async()=>{
    try{await navigator.clipboard.writeText(text);toast("Report copied to clipboard.");}
    catch(error){toast("Clipboard is unavailable in this browser.");}
  });
  actions.append(download,copy);results.append(head,preview,actions);
  results.scrollIntoView({behavior:"smooth",block:"nearest"});
});
$("hardware-reset").addEventListener("click",()=>{
  ["hw-cpu","hw-gpu","hw-board","hw-wireless","hw-storage","hw-other","hw-raw"].forEach(id=>$(id).value="");
  $("hardware-results").hidden=true;$("hardware-results").replaceChildren();
});
})();