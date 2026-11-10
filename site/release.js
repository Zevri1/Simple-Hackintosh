/* Live GitHub Release metadata: the website follows the newest published stable release. */
(() => {
  const API = "https://api.github.com/repos/Zevri1/Simple-Hackintosh/releases/latest";
  const FALLBACK = {
    tag_name:"v2.5.2",
    html_url:"https://github.com/Zevri1/Simple-Hackintosh/releases/tag/v2.5.2",
    assets:[{name:"Simple-Hackintosh-CLI-v2.5.2.zip",browser_download_url:"https://github.com/Zevri1/Simple-Hackintosh/releases/download/v2.5.2/Simple-Hackintosh-CLI-v2.5.2.zip"}]
  };
  const version = tag => String(tag || "").replace(/^v/i,"");
  const asset = release => (release.assets || []).find(a => /simple-hackintosh.*\.zip$/i.test(a.name) && /cli/i.test(a.name))
    || (release.assets || []).find(a => /\.zip$/i.test(a.name));
  function apply(release) {
    const tag = release.tag_name || "latest";
    const v = version(tag);
    const download = asset(release)?.browser_download_url || release.html_url;
    document.querySelectorAll("[data-release-download]").forEach(a => { a.href=download; a.setAttribute("aria-label","Download Simple Hackintosh CLI "+v); });
    document.querySelectorAll("[data-release-notes]").forEach(a => { a.href=release.html_url; });
    document.querySelectorAll("[data-release-version]").forEach(el => { el.textContent=v; });
    document.querySelectorAll("[data-release-tag]").forEach(el => { el.textContent=tag; });
    const status=document.getElementById("release-status");
    if(status){status.textContent="Live from GitHub Releases · "+tag;status.dataset.state="live";}
    const pill=document.getElementById("release-live-pill");
    if(pill){pill.hidden=false;}
  }
  async function load(){
    try {
      const response=await fetch(API,{headers:{Accept:"application/vnd.github+json"},cache:"no-store"});
      if(!response.ok) throw new Error("GitHub API "+response.status);
      const release=await response.json();
      if(!release || release.draft || release.prerelease || !release.tag_name) throw new Error("No stable release");
      apply(release);
    } catch(error) {
      apply(FALLBACK);
      const status=document.getElementById("release-status");
      if(status) status.textContent="Showing last known release · live check unavailable";
    }
  }
  if(document.readyState==="loading") document.addEventListener("DOMContentLoaded",load,{once:true}); else load();
})();
