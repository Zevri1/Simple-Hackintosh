const chapters = [
  {id:"00-read-first",title:"Read this first",group:"START HERE",filter:"start",description:"Scope, prerequisites, risks, and the right way to approach the project."},
  {id:"01-hardware-inventory",title:"Hardware inventory",group:"START HERE",filter:"start",description:"Collect exact component names, IDs, firmware details, and system information."},
  {id:"02-compatibility",title:"Compatibility check",group:"START HERE",filter:"start",description:"Assess the parts that matter before choosing an installation path."},
  {id:"03-usb-installer-windows",title:"Windows USB installer",group:"BUILD YOUR EFI",filter:"build",description:"Understand the Recovery installer workflow from a Windows environment."},
  {id:"04-efi-structure",title:"EFI structure",group:"BUILD YOUR EFI",filter:"build",description:"Learn what belongs in EFI, OC, ACPI, Drivers, Kexts, and Tools."},
  {id:"05-acpi-kexts-drivers",title:"ACPI, Kexts & drivers",group:"BUILD YOUR EFI",filter:"build",description:"Understand the role of each supporting component and its dependencies."},
  {id:"06-config-plist",title:"Configure config.plist",group:"BUILD YOUR EFI",filter:"build",description:"Build a configuration for the hardware you actually have."},
  {id:"07-validation-debugging",title:"Validation & debugging",group:"BUILD YOUR EFI",filter:"build",description:"Validate structure, catch mistakes, and read useful diagnostic output."},
  {id:"08-installation",title:"Install macOS",group:"INSTALL & VERIFY",filter:"install",description:"Prepare carefully, boot Recovery, and understand the installation stages."},
  {id:"09-post-installation",title:"Post-installation",group:"INSTALL & VERIFY",filter:"install",description:"Check hardware, services, stability, and expected functionality after boot."},
  {id:"10-usb-mapping",title:"USB mapping",group:"INSTALL & VERIFY",filter:"install",description:"Understand physical port mapping and the limits of USB configuration."},
  {id:"11-macos-tahoe-26",title:"macOS Tahoe 26",group:"INSTALL & VERIFY",filter:"install",description:"Review release-specific notes and compatibility considerations."},
  {id:"12-dual-boot",title:"Dual boot safety",group:"MAINTAIN & LEARN",filter:"maintain",description:"Protect existing installations, boot entries, and important files."},
  {id:"13-maintenance",title:"Maintenance & updates",group:"MAINTAIN & LEARN",filter:"maintain",description:"Keep a known-good EFI and update one component at a time."},
  {id:"14-troubleshooting",title:"Troubleshooting",group:"MAINTAIN & LEARN",filter:"maintain",description:"Narrow failures to a stage and gather evidence before changing settings."},
  {id:"15-glossary",title:"Glossary",group:"MAINTAIN & LEARN",filter:"maintain",description:"A reference for OpenCore, firmware, ACPI, storage, and macOS terms."},
  {id:"16-visual-walkthrough",title:"Visual walkthrough",group:"MAINTAIN & LEARN",filter:"maintain",description:"Follow the illustrated structure and connect the main concepts."}
];

const homeView = document.getElementById("home-view");
const docsView = document.getElementById("docs-view");
const toolsView = document.getElementById("tools-view");
const article = document.getElementById("doc-article");
const chapterGrid = document.getElementById("chapter-grid");
const chapterNav = document.getElementById("chapter-nav");
const tocLinks = document.getElementById("toc-links");
const pagination = document.getElementById("chapter-pagination");
const progressFill = document.getElementById("chapter-progress");
const progressLabel = document.getElementById("progress-label");
const completedCount = document.getElementById("completed-count");
const searchDialog = document.getElementById("search-dialog");
const globalSearch = document.getElementById("global-search");
const globalResults = document.getElementById("global-results");
const docsSearch = document.getElementById("docs-search");
const readingProgress = document.getElementById("reading-progress");
const announcer = document.getElementById("site-announcer");
const topNav = document.getElementById("top-nav");
const menuButton = document.getElementById("mobile-menu");
const sidebarToggle = document.getElementById("sidebar-toggle");
const docsSidebar = document.getElementById("docs-sidebar");
const breadcrumbTitle = document.getElementById("breadcrumb-title");
const themeButton = document.getElementById("theme-toggle");

let activeChapter = null;
let loadSequence = 0;
let searchIndex = null;
let searchPromise = null;
let headingObserver = null;
let progressFrame = null;
let completed = loadCompleted();

function loadCompleted() {
  try {
    const saved = JSON.parse(localStorage.getItem("simple-hackintosh-completed") || "[]");
    return new Set(Array.isArray(saved) ? saved.filter(id => chapters.some(chapter => chapter.id === id)) : []);
  } catch (error) {
    return new Set();
  }
}

function saveCompleted() {
  try { localStorage.setItem("simple-hackintosh-completed", JSON.stringify(Array.from(completed))); }
  catch (error) { announcer.textContent = "Progress could not be saved in this browser."; }
}

function escapeHTML(value) {
  return String(value).replace(/[&<>"']/g, function (character) {
    return {"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[character];
  });
}

function safeHref(value, forImage) {
  const raw = String(value).replace(/&amp;/g, "&").trim();
  if (!raw || /[\u0000-\u001f\u007f]/.test(raw)) return "#";
  if (/^https?:\/\//i.test(raw)) return escapeHTML(raw);
  if (!forImage && /^mailto:/i.test(raw)) return escapeHTML(raw);
  if (/^(#|\/(?!\/)|\.{1,2}\/|[a-z0-9_-]+\.md(?:#.*)?)$/i.test(raw)) return escapeHTML(raw);
  return "#";
}

function slugify(value) {
  return value.toLowerCase().trim().replace(/[^a-z0-9\s-]/g, "").replace(/\s+/g, "-").replace(/-+/g, "-") || "section";
}

function uniqueSlug(value, used) {
  const base = slugify(value);
  let id = base;
  let number = 2;
  while (used.has(id)) id = base + "-" + number++;
  used.add(id);
  return id;
}

function inlineMarkdown(input) {
  let text = escapeHTML(input);
  const codeTokens = [];
  text = text.replace(/\x60([^\x60]+)\x60/g, function (_, code) {
    const key = "%%INLINECODE" + codeTokens.length + "%%";
    codeTokens.push("<code>" + code + "</code>");
    return key;
  });
  text = text.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, function (_, alt, url) {
    return '<img loading="lazy" alt="' + alt + '" src="' + safeHref(url, true) + '">';
  });
  text = text.replace(/\[([^\]]+)\]\(([^)]+)\)/g, function (_, label, url) {
    const href = safeHref(url, false);
    const external = /^https?:\/\//i.test(url);
    return '<a href="' + href + '"' + (external ? ' target="_blank" rel="noopener noreferrer"' : "") + ">" + label + "</a>";
  });
  text = text.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
  text = text.replace(/\*([^*]+)\*/g, "<em>$1</em>");
  text = text.replace(/~~(.+?)~~/g, "<del>$1</del>");
  codeTokens.forEach(function (html, index) { text = text.replace("%%INLINECODE" + index + "%%", html); });
  return text;
}

function parseTableRow(line) {
  return line.trim().replace(/^\|/, "").replace(/\|$/, "").split("|").map(cell => cell.trim());
}

function renderMarkdown(markdown) {
  const lines = markdown.replace(/\r/g, "").split("\n");
  const usedHeadingIds = new Set();
  let html = "";
  let paragraph = [];
  let listType = "";
  let quoteLines = [];
  let codeLines = [];
  let inCode = false;

  const flushParagraph = () => {
    if (paragraph.length) {
      html += "<p>" + inlineMarkdown(paragraph.join(" ")) + "</p>";
      paragraph = [];
    }
  };
  const closeList = () => {
    if (listType) { html += "</" + listType + ">"; listType = ""; }
  };
  const flushQuote = () => {
    if (quoteLines.length) {
      html += "<blockquote>" + quoteLines.map(line => "<p>" + inlineMarkdown(line.replace(/^>\s?/, "")) + "</p>").join("") + "</blockquote>";
      quoteLines = [];
    }
  };

  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];
    if (line.trim().startsWith("```")) {
      flushParagraph(); closeList(); flushQuote();
      if (!inCode) { inCode = true; codeLines = []; }
      else { html += "<pre><code>" + escapeHTML(codeLines.join("\n")) + "</code></pre>"; inCode = false; }
      continue;
    }
    if (inCode) { codeLines.push(line); continue; }
    if (/^>/.test(line)) {
      flushParagraph(); closeList(); quoteLines.push(line); continue;
    } else flushQuote();
    if (!line.trim()) { flushParagraph(); closeList(); continue; }
    if (/^\s*---+\s*$/.test(line)) { flushParagraph(); closeList(); html += "<hr>"; continue; }

    const heading = line.match(/^(#{1,4})\s+(.+)$/);
    if (heading) {
      flushParagraph(); closeList();
      const level = heading[1].length;
      const title = heading[2].replace(/\s+#+\s*$/, "");
      const id = uniqueSlug(title, usedHeadingIds);
      html += "<h" + level + ' id="' + id + '">' + inlineMarkdown(title) + "</h" + level + ">";
      continue;
    }

    if (/^\s*\|/.test(line) && index + 1 < lines.length && /^\s*\|?\s*:?-{3,}/.test(lines[index + 1])) {
      flushParagraph(); closeList();
      const headers = parseTableRow(line);
      index += 2;
      let rows = "";
      while (index < lines.length && /^\s*\|/.test(lines[index])) {
        const cells = parseTableRow(lines[index]);
        rows += "<tr>" + headers.map((_, cellIndex) => "<td>" + inlineMarkdown(cells[cellIndex] || "") + "</td>").join("") + "</tr>";
        index++;
      }
      index--;
      html += "<div class=\"table-wrap\"><table><thead><tr>" + headers.map(cell => "<th>" + inlineMarkdown(cell) + "</th>").join("") + "</tr></thead><tbody>" + rows + "</tbody></table></div>";
      continue;
    }

    const unordered = line.match(/^\s*[-*+]\s+(.+)$/);
    const ordered = line.match(/^\s*\d+\.\s+(.+)$/);
    if (unordered || ordered) {
      flushParagraph();
      const type = unordered ? "ul" : "ol";
      if (listType !== type) { closeList(); listType = type; html += "<" + type + ">"; }
      let item = (unordered || ordered)[1];
      item = item.replace(/^\[ \]\s+/, "☐ ").replace(/^\[x\]\s+/i, "☑ ");
      html += "<li>" + inlineMarkdown(item) + "</li>";
      continue;
    }
    closeList();
    paragraph.push(line.trim());
  }

  flushParagraph(); closeList(); flushQuote();
  if (inCode) html += "<pre><code>" + escapeHTML(codeLines.join("\n")) + "</code></pre>";
  return html;
}

function updateProgress() {
  const total = chapters.length;
  const done = completed.size;
  const percent = Math.round((done / total) * 100);
  progressFill.style.width = percent + "%";
  progressLabel.textContent = done + " / " + total;
  completedCount.textContent = String(done).padStart(2, "0");
}

function renderChapterNavigation(filterText) {
  const filter = (filterText || "").trim().toLowerCase();
  chapterNav.replaceChildren();
  const groups = ["START HERE", "BUILD YOUR EFI", "INSTALL & VERIFY", "MAINTAIN & LEARN"];
  groups.forEach(function (group) {
    const items = chapters.filter(chapter => chapter.group === group && (!filter || (chapter.title + " " + chapter.description).toLowerCase().includes(filter)));
    if (!items.length) return;
    const section = document.createElement("div");
    section.className = "nav-group";
    const label = document.createElement("span");
    label.className = "nav-label";
    label.textContent = group;
    section.appendChild(label);
    items.forEach(function (chapter) {
      const link = document.createElement("a");
      link.className = "chapter-link";
      link.href = "#chapter/" + chapter.id;
      link.dataset.chapter = chapter.id;
      const number = document.createElement("span");
      number.className = "chapter-number";
      number.textContent = chapter.id.slice(0, 2);
      const title = document.createElement("span");
      title.textContent = chapter.title;
      link.append(number, title);
      if (completed.has(chapter.id)) link.classList.add("is-complete");
      if (activeChapter === chapter.id) link.classList.add("active");
      section.appendChild(link);
    });
    chapterNav.appendChild(section);
  });
  if (!chapterNav.children.length) {
    const empty = document.createElement("p");
    empty.className = "nav-empty";
    empty.textContent = "No matching chapters.";
    chapterNav.appendChild(empty);
  }
}

function renderChapterCards(filter) {
  const selected = filter || "all";
  const visible = chapters.filter(chapter => selected === "all" || chapter.filter === selected);
  chapterGrid.replaceChildren();
  visible.forEach(function (chapter) {
    const card = document.createElement("a");
    card.className = "chapter-card" + (completed.has(chapter.id) ? " is-complete" : "");
    card.href = "#chapter/" + chapter.id;
    const top = document.createElement("div");
    top.className = "chapter-card-top";
    const number = document.createElement("span");
    number.className = "chapter-card-number";
    number.textContent = chapter.id.slice(0, 2);
    const group = document.createElement("span");
    group.className = "chapter-card-group";
    group.textContent = chapter.group;
    top.append(number, group);
    const title = document.createElement("h3");
    title.textContent = chapter.title;
    const description = document.createElement("p");
    description.textContent = chapter.description;
    const bottom = document.createElement("div");
    bottom.className = "chapter-card-bottom";
    const status = document.createElement("span");
    status.textContent = completed.has(chapter.id) ? "COMPLETED" : "OPEN CHAPTER";
    const arrow = document.createElement("b");
    arrow.textContent = "↗";
    bottom.append(status, arrow);
    card.append(top, title, description, bottom);
    chapterGrid.appendChild(card);
  });
}

function setTopNavigation(isHome, isTools) {
  document.querySelector("[data-home-link]").classList.toggle("active", isHome);
  document.querySelector("[data-guide-link]").classList.toggle("active", !isHome && !isTools);
  document.querySelector("[data-tools-link]").classList.toggle("active", !!isTools);
}

function setMobileMenu(open) {
  document.body.classList.toggle("nav-open", open);
  menuButton.setAttribute("aria-expanded", String(open));
  menuButton.setAttribute("aria-label", open ? "Close navigation" : "Open navigation");
}

function setDocsMenu(open) {
  document.body.classList.toggle("docs-menu-open", open);
  sidebarToggle.setAttribute("aria-expanded", String(open));
  sidebarToggle.textContent = open ? "× Close chapters" : "☷ Chapters";
}

function setActiveChapter(id) {
  document.querySelectorAll(".chapter-link").forEach(function (link) {
    const active = link.dataset.chapter === id;
    link.classList.toggle("active", active);
    if (active) link.setAttribute("aria-current", "page");
    else link.removeAttribute("aria-current");
  });
}

async function copyText(value) {
  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(value);
      return true;
    }
  } catch (error) {}
  const field = document.createElement("textarea");
  field.value = value;
  field.setAttribute("readonly", "");
  field.style.position = "fixed";
  field.style.opacity = "0";
  document.body.appendChild(field);
  field.select();
  let copied = false;
  try { copied = document.execCommand("copy"); } catch (error) {}
  field.remove();
  return copied;
}

function addCopyControls() {
  article.querySelectorAll("pre").forEach(function (pre) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "copy-code";
    button.textContent = "Copy code";
    button.addEventListener("click", async function () {
      const code = pre.querySelector("code");
      const ok = await copyText(code ? code.textContent : pre.textContent);
      button.textContent = ok ? "Copied" : "Select manually";
      announcer.textContent = ok ? "Code copied." : "Clipboard access was unavailable.";
      window.setTimeout(() => { button.textContent = "Copy code"; }, 1800);
    });
    pre.appendChild(button);
  });
  article.querySelectorAll("h2[id], h3[id]").forEach(function (heading) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "heading-anchor";
    button.textContent = "#";
    button.title = "Copy a link to this section";
    button.setAttribute("aria-label", "Copy link to " + heading.textContent);
    button.addEventListener("click", async function () {
      const url = window.location.origin + window.location.pathname + "#chapter/" + activeChapter + "/" + encodeURIComponent(heading.id);
      const ok = await copyText(url);
      announcer.textContent = ok ? "Section link copied." : "Could not copy the section link.";
    });
    heading.appendChild(button);
  });
}

function renderToc() {
  tocLinks.replaceChildren();
  const headings = article.querySelectorAll("h2[id], h3[id]");
  headings.forEach(function (heading) {
    const link = document.createElement("a");
    link.className = heading.tagName === "H3" ? "toc-sub" : "";
    link.href = "#" + heading.id;
    link.textContent = heading.textContent.replace("#", "").trim();
    tocLinks.appendChild(link);
  });
  if (headingObserver) headingObserver.disconnect();
  if ("IntersectionObserver" in window) {
    headingObserver = new IntersectionObserver(function (entries) {
      const visible = entries.filter(entry => entry.isIntersecting).sort((a,b) => a.boundingClientRect.top - b.boundingClientRect.top);
      if (!visible.length) return;
      const id = visible[0].target.id;
      tocLinks.querySelectorAll("a").forEach(link => {
        const active = link.getAttribute("href") === "#" + id;
        link.classList.toggle("active", active);
        if (active) link.setAttribute("aria-current", "location");
        else link.removeAttribute("aria-current");
      });
    }, {rootMargin:"-16% 0px -70% 0px",threshold:0});
    headings.forEach(heading => headingObserver.observe(heading));
  }
}

function renderPagination(index) {
  pagination.replaceChildren();
  const previous = chapters[index - 1];
  const next = chapters[index + 1];
  if (previous) {
    const link = document.createElement("a");
    link.className = "page-nav";
    link.href = "#chapter/" + previous.id;
    link.innerHTML = "<small>← PREVIOUS CHAPTER</small><b></b>";
    link.querySelector("b").textContent = previous.title;
    pagination.appendChild(link);
  }
  if (next) {
    const link = document.createElement("a");
    link.className = "page-nav next";
    link.href = "#chapter/" + next.id;
    link.innerHTML = "<small>NEXT CHAPTER →</small><b></b>";
    link.querySelector("b").textContent = next.title;
    pagination.appendChild(link);
  }
}

function updateReadingProgress() {
  const scrollable = document.documentElement.scrollHeight - window.innerHeight;
  const value = scrollable > 0 ? Math.max(0, Math.min(100, window.scrollY / scrollable * 100)) : 0;
  readingProgress.querySelector("span").style.transform = "scaleX(" + (value / 100) + ")";
}

function setChapterCompletion(id, done) {
  if (done) completed.add(id);
  else completed.delete(id);
  saveCompleted();
  updateProgress();
  renderChapterCards(document.querySelector(".filter-pill.active")?.dataset.filter || "all");
  renderChapterNavigation(docsSearch.value);
  const button = article.querySelector("[data-complete-chapter]");
  if (button) {
    button.textContent = done ? "✓ Completed — mark unread" : "Mark chapter as complete";
    button.setAttribute("aria-pressed", String(done));
  }
}

function showHome() {
  loadSequence++;
  activeChapter = null;
  homeView.hidden = false;
  docsView.hidden = true;
  toolsView.hidden = true;
  setTopNavigation(true, false);
  setDocsMenu(false);
  setMobileMenu(false);
  document.title = "Simple Hackintosh — OpenCore Field Guide";
}

async function openChapter(id, anchorId) {
  const chapter = chapters.find(item => item.id === id);
  if (!chapter) { showHome(); return; }
  const requestId = ++loadSequence;
  activeChapter = id;
  homeView.hidden = true;
  docsView.hidden = false;
  toolsView.hidden = true;
  setTopNavigation(false, false);
  setMobileMenu(false);
  setDocsMenu(false);
  renderChapterNavigation(docsSearch.value);
  breadcrumbTitle.textContent = chapter.title;
  article.innerHTML = '<div class="loading-state"><span class="loading-dot"></span><p>Loading chapter…</p></div>';
  tocLinks.replaceChildren();
  pagination.replaceChildren();
  window.scrollTo({top:0,behavior:"auto"});
  try {
    const response = await fetch("docs/" + id + ".md", {cache:"no-cache"});
    if (!response.ok) throw new Error("Chapter request failed (HTTP " + response.status + ").");
    const markdown = await response.text();
    if (requestId !== loadSequence) return;
    article.innerHTML = renderMarkdown(markdown);
    const index = chapters.findIndex(item => item.id === id);
    const wordCount = markdown.trim().split(/\s+/).filter(Boolean).length;
    const minutes = Math.max(1, Math.ceil(wordCount / 220));
    const meta = document.createElement("div");
    meta.className = "article-meta";
    const label = document.createElement("span");
    label.textContent = "CHAPTER " + String(index + 1).padStart(2,"0") + " / " + String(chapters.length).padStart(2,"0") + " · " + minutes + " MIN READ";
    const completeButton = document.createElement("button");
    completeButton.type = "button";
    completeButton.dataset.completeChapter = "true";
    const isDone = completed.has(id);
    completeButton.textContent = isDone ? "✓ Completed — mark unread" : "Mark chapter as complete";
    completeButton.setAttribute("aria-pressed", String(isDone));
    completeButton.addEventListener("click", () => setChapterCompletion(id, !completed.has(id)));
    meta.append(label, completeButton);
    article.prepend(meta);

    const h1 = article.querySelector("h1");
    if (h1) { h1.tabIndex = -1; h1.focus({preventScroll:true}); }
    addCopyControls();
    renderToc();
    renderPagination(index);
    setActiveChapter(id);
    updateProgress();
    document.title = chapter.title + " — Simple Hackintosh";
    updateReadingProgress();
    if (anchorId) {
      let targetId = anchorId;
      try { targetId = decodeURIComponent(anchorId); } catch (error) {}
      window.setTimeout(function () {
        const target = document.getElementById(targetId);
        if (target && article.contains(target)) target.scrollIntoView({behavior:"smooth",block:"start"});
      }, 100);
    }
  } catch (error) {
    if (requestId !== loadSequence) return;
    article.innerHTML = '<div class="error-state"><h2>Chapter could not be loaded</h2><p>' + escapeHTML(error.message) + '</p><p><a href="https://github.com/kiwewe09-aa/Simple-Hackintosh/blob/main/docs/' + id + '.md" target="_blank" rel="noopener noreferrer">Open the source chapter on GitHub ↗</a></p></div>';
  }
}

function openTools(toolName) {
  const allowed = ["hardware", "efi", "troubleshoot"];
  const selected = allowed.includes(toolName) ? toolName : "hardware";
  loadSequence++;
  activeChapter = null;
  homeView.hidden = true;
  docsView.hidden = true;
  toolsView.hidden = false;
  setTopNavigation(false, true);
  setMobileMenu(false);
  setDocsMenu(false);
  document.querySelectorAll("[data-tool-select]").forEach(function (button) {
    const active = button.dataset.toolSelect === selected;
    button.classList.toggle("active", active);
    button.setAttribute("aria-pressed", String(active));
  });
  document.querySelectorAll("[data-tool-panel]").forEach(function (panel) {
    panel.hidden = panel.dataset.toolPanel !== selected;
    panel.classList.toggle("active", panel.dataset.toolPanel === selected);
  });
  document.title = "Tool Center — Simple Hackintosh";
  window.scrollTo({top:0,behavior:"auto"});
}

function route() {
  const hash = window.location.hash || "#home";
  if (hash === "#tools" || hash.startsWith("#tools/")) {
    const requestedTool = hash.startsWith("#tools/") ? hash.slice("#tools/".length).split("/")[0] : "hardware";
    openTools(requestedTool);
  } else if (hash.startsWith("#chapter/")) {
    const parts = hash.slice("#chapter/".length).split("/");
    const id = parts.shift();
    const anchor = parts.length ? parts.join("/") : null;
    openChapter(id, anchor);
  } else if (hash === "#guide") {
    openChapter("00-read-first");
  } else {
    showHome();
    if (hash === "#chapters" || hash === "#library") {
      window.setTimeout(() => document.getElementById("chapters").scrollIntoView({behavior:"smooth"}), 0);
    }
  }
}

async function buildSearchIndex() {
  if (searchIndex) return searchIndex;
  if (searchPromise) return searchPromise;
  searchPromise = Promise.allSettled(chapters.map(async function (chapter) {
    const response = await fetch("docs/" + chapter.id + ".md");
    if (!response.ok) throw new Error("Search index request failed");
    const markdown = await response.text();
    const plain = markdown.replace(/\x60{3}[\s\S]*?\x60{3}/g, " ")
      .replace(/!\[([^\]]*)\]\([^)]+\)/g, "$1")
      .replace(/\[([^\]]+)\]\([^)]+\)/g, "$1")
      .replace(/[>#*_|\x60\[\]()!-]/g, " ")
      .replace(/\s+/g, " ").toLowerCase();
    return {id:chapter.id,title:chapter.title,text:plain};
  })).then(function (results) {
    searchIndex = results.filter(result => result.status === "fulfilled").map(result => result.value);
    searchPromise = null;
    if (!searchIndex.length) throw new Error("Search index is unavailable");
    return searchIndex;
  }).catch(function (error) { searchPromise = null; throw error; });
  return searchPromise;
}

async function runGlobalSearch(query) {
  const value = query.trim().toLowerCase();
  globalResults.replaceChildren();
  if (!value) {
    const hint = document.createElement("p");
    hint.textContent = "Type to search chapter titles and content.";
    globalResults.appendChild(hint);
    return;
  }
  const loading = document.createElement("p");
  loading.textContent = "Searching the field guide…";
  globalResults.appendChild(loading);
  try {
    const index = await buildSearchIndex();
    if (globalSearch.value.trim().toLowerCase() !== value) return;
    const matches = index.filter(item => item.title.toLowerCase().includes(value) || item.text.includes(value)).slice(0, 8);
    globalResults.replaceChildren();
    if (!matches.length) {
      const empty = document.createElement("p");
      empty.textContent = "No matching chapter. Try a hardware name, ACPI, Kext, or config.plist.";
      globalResults.appendChild(empty);
      return;
    }
    matches.forEach(function (item) {
      const link = document.createElement("a");
      link.className = "global-result";
      link.href = "#chapter/" + item.id;
      const title = document.createElement("strong");
      title.textContent = item.title;
      const position = item.text.indexOf(value);
      const start = Math.max(0, position - 55);
      const end = position < 0 ? Math.min(item.text.length, 110) : Math.min(item.text.length, position + value.length + 75);
      const snippet = document.createElement("small");
      snippet.textContent = (start ? "…" : "") + item.text.slice(start,end) + (end < item.text.length ? "…" : "");
      link.append(title,snippet);
      globalResults.appendChild(link);
    });
  } catch (error) {
    globalResults.replaceChildren();
    const failed = document.createElement("p");
    failed.textContent = "Search is temporarily unavailable. Check your connection and try again.";
    globalResults.appendChild(failed);
  }
}

function openSearch() {
  if (typeof searchDialog.showModal === "function") searchDialog.showModal();
  else searchDialog.setAttribute("open", "");
  window.setTimeout(() => globalSearch.focus(), 30);
}
function closeSearch() {
  if (typeof searchDialog.close === "function" && searchDialog.open) searchDialog.close();
  else searchDialog.removeAttribute("open");
}

document.getElementById("chapter-filters").addEventListener("click", function (event) {
  const button = event.target.closest("[data-filter]");
  if (!button) return;
  document.querySelectorAll(".filter-pill").forEach(function (pill) {
    const active = pill === button;
    pill.classList.toggle("active", active);
    pill.setAttribute("aria-pressed", String(active));
  });
  renderChapterCards(button.dataset.filter);
});

document.getElementById("search-open").addEventListener("click", openSearch);
document.getElementById("search-close").addEventListener("click", closeSearch);
globalResults.addEventListener("click", function (event) {
  if (event.target.closest("a")) closeSearch();
});
searchDialog.addEventListener("click", function (event) {
  if (event.target === searchDialog) closeSearch();
});
globalSearch.addEventListener("input", function () { runGlobalSearch(globalSearch.value); });
globalSearch.addEventListener("keydown", function (event) {
  if (event.key === "Enter") {
    const first = globalResults.querySelector("a");
    if (first) { window.location.hash = first.getAttribute("href"); closeSearch(); }
  }
});


const hardwareForm = document.getElementById("hardware-report-form");
const reportOutput = document.getElementById("report-output");
const reportPreview = document.getElementById("hardware-report-preview");
let latestHardwareReport = "";

hardwareForm.addEventListener("submit", function (event) {
  event.preventDefault();
  const values = Object.fromEntries(new FormData(hardwareForm).entries());
  const fields = [
    ["Computer / board model", values.model],
    ["CPU", values.cpu],
    ["Firmware mode", values.firmware],
    ["Memory", values.memory],
    ["Integrated graphics", values.igpu],
    ["Dedicated graphics", values.dgpu],
    ["Wi-Fi / Bluetooth", values.wireless],
    ["Ethernet", values.ethernet],
    ["Storage controller / drive", values.storage],
    ["Target macOS", values.macos],
    ["Current operating system", values.currentOs],
    ["PCI IDs / additional notes", values.notes]
  ];
  latestHardwareReport = [
    "# Hardware Inventory Report",
    "",
    "> Generated by Simple Hackintosh Tool Center. This report contains only details entered by the user.",
    "",
    "**Date:** " + new Date().toISOString().slice(0,10),
    "",
    ...fields.map(([label,value]) => "**" + label + ":** " + (String(value || "").trim() || "Not provided")),
    "",
    "## Before sharing",
    "- Verify component names and hardware IDs.",
    "- Remove serial numbers, UUIDs, account names, and other private identifiers.",
    "- Unknown details should remain marked as unknown rather than guessed."
  ].join("\n");
  reportPreview.textContent = latestHardwareReport;
  reportOutput.hidden = false;
  announcer.textContent = "Hardware report generated.";
  reportOutput.scrollIntoView({behavior:"smooth",block:"nearest"});
});

document.getElementById("copy-hardware-report").addEventListener("click", async function () {
  const ok = await copyText(latestHardwareReport);
  this.textContent = ok ? "Copied" : "Copy unavailable";
  announcer.textContent = ok ? "Hardware report copied." : "Could not copy the report.";
  window.setTimeout(() => { this.textContent = "Copy report"; }, 1800);
});

document.getElementById("download-hardware-report").addEventListener("click", function () {
  if (!latestHardwareReport) return;
  const blob = new Blob([latestHardwareReport], {type:"text/markdown;charset=utf-8"});
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "simple-hackintosh-hardware-report.md";
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
});

hardwareForm.addEventListener("reset", function () {
  reportOutput.hidden = true;
  latestHardwareReport = "";
  reportPreview.textContent = "";
});


const efiInput = document.getElementById("efi-folder-input");
const efiSelected = document.getElementById("efi-selected");
const efiFileCount = document.getElementById("efi-file-count");
const efiRunButton = document.getElementById("run-efi-check");
const efiResults = document.getElementById("efi-results");
let selectedEfiFiles = [];

efiInput.addEventListener("change", function () {
  selectedEfiFiles = Array.from(efiInput.files || []);
  efiSelected.hidden = selectedEfiFiles.length === 0;
  efiFileCount.textContent = selectedEfiFiles.length + " files selected";
  efiRunButton.disabled = selectedEfiFiles.length === 0;
  efiResults.hidden = true;
});

document.getElementById("efi-clear").addEventListener("click", function () {
  efiInput.value = "";
  selectedEfiFiles = [];
  efiSelected.hidden = true;
  efiRunButton.disabled = true;
  efiResults.hidden = true;
  efiResults.replaceChildren();
});

function normalizeEfiPath(path) {
  return String(path || "").replace(/\\/g,"/").split("/").filter(Boolean).join("/").toLowerCase();
}

function parsePlistNode(node) {
  const tag = node.tagName.toLowerCase();
  if (tag === "dict") {
    const children = Array.from(node.children);
    const output = {};
    for (let i=0;i<children.length;i+=2) {
      if (children[i].tagName.toLowerCase() !== "key" || !children[i+1]) continue;
      output[children[i].textContent] = parsePlistNode(children[i+1]);
    }
    return output;
  }
  if (tag === "array") return Array.from(node.children).map(parsePlistNode);
  if (tag === "true") return true;
  if (tag === "false") return false;
  if (tag === "integer") return Number.parseInt(node.textContent,10);
  if (tag === "real") return Number.parseFloat(node.textContent);
  if (tag === "string" || tag === "date" || tag === "data") return node.textContent;
  return null;
}

function getPlistPath(object, path) {
  return path.reduce((value,key) => value && typeof value === "object" ? value[key] : undefined, object);
}

function renderEfiChecks(checks, totalFiles) {
  efiResults.replaceChildren();
  const passed = checks.filter(item=>item.status==="pass").length;
  const failed = checks.filter(item=>item.status==="fail").length;
  const warnings = checks.filter(item=>item.status==="warn").length;
  const summary = document.createElement("div");
  summary.className = "efi-summary";
  [[passed,"PASSED"],[failed,"NEEDS ATTENTION"],[warnings,"WARNINGS"]].forEach(([value,label])=>{
    const box=document.createElement("div");
    const strong=document.createElement("strong"); strong.textContent=String(value).padStart(2,"0");
    const small=document.createElement("span"); small.textContent=label;
    box.append(strong,small); summary.appendChild(box);
  });
  efiResults.appendChild(summary);
  const list=document.createElement("div"); list.className="efi-check-list";
  checks.forEach(item=>{
    const row=document.createElement("div"); row.className="efi-check "+item.status;
    const mark=document.createElement("span"); mark.className="efi-check-mark"; mark.textContent=item.status==="pass"?"✓":item.status==="fail"?"!":"i";
    const copy=document.createElement("div");
    const title=document.createElement("b"); title.textContent=item.title;
    const message=document.createElement("p"); message.textContent=item.message;
    copy.append(title,message); row.append(mark,copy); list.appendChild(row);
  });
  efiResults.appendChild(list);
  const note=document.createElement("p"); note.className="trouble-note";
  note.textContent="Inspected "+totalFiles+" selected files in this browser session. Nothing was uploaded. This structural review is not a boot guarantee or an ocvalidate replacement.";
  efiResults.appendChild(note);
  efiResults.hidden=false;
}

async function inspectEfiFolder() {
  if (!selectedEfiFiles.length) return;
  efiRunButton.disabled=true;
  efiRunButton.textContent="Inspecting…";
  const checks=[];
  const add=(status,title,message)=>checks.push({status,title,message});
  try {
    const mapped=selectedEfiFiles.map(file=>{
      const original=file.webkitRelativePath || file.name;
      const parts=original.replace(/\\/g,"/").split("/").filter(Boolean);
      const efiAt=parts.findIndex(part=>part.toLowerCase()==="efi");
      const relative=efiAt>=0 ? parts.slice(efiAt).join("/") : original;
      return {file,path:normalizeEfiPath(relative)};
    });
    const paths=mapped.map(item=>item.path);
    const has=(path)=>paths.includes(normalizeEfiPath(path)) || paths.some(value=>value.startsWith(normalizeEfiPath(path)+"/"));
    const hasEfiRoot=paths.some(path=>path.startsWith("efi/"));
    if (!hasEfiRoot) {
      add("fail","EFI folder root not detected","Choose the EFI folder itself, or a parent folder that contains a directory named EFI. The selected files do not expose the expected EFI/ path.");
    } else {
      add("pass","EFI folder root detected","The selected files include an EFI/ directory.");
    }

    [
      ["efi/boot/bootx64.efi","UEFI fallback boot file"],
      ["efi/oc/opencore.efi","OpenCore.efi"],
      ["efi/oc/config.plist","config.plist"],
      ["efi/oc/drivers/openruntime.efi","OpenRuntime.efi"]
    ].forEach(([path,label])=>{
      const ok=has(path);
      add(ok?"pass":"fail",label,ok?"Expected file is present.":"Expected path was not found: "+path+". Check the folder structure and file name.");
    });

    const config=mapped.find(item=>item.path==="efi/oc/config.plist");
    if (!config) {
      add("fail","config.plist inspection skipped","The inspector cannot check section types or referenced paths until EFI/OC/config.plist is present.");
    } else {
      try {
        const content=await config.file.text();
        if (content.startsWith("bplist00")) {
          add("warn","Binary config.plist detected","This version of the inspector parses XML plist files only. Export config.plist as XML with a trusted plist editor to inspect its sections and path references.");
        } else {
          const xml=new DOMParser().parseFromString(content,"application/xml");
          if (xml.querySelector("parsererror")) {
            add("fail","config.plist XML syntax","The file could not be parsed as well-formed XML.");
          } else {
            const plist=xml.documentElement;
            const root=plist && Array.from(plist.children).find(node=>node.tagName.toLowerCase()==="dict");
            if (!root) {
              add("fail","config.plist root","Expected a plist document with a dictionary as its root value.");
            } else {
              const configObject=parsePlistNode(root);
              add("pass","config.plist XML syntax","The XML is well-formed and has a dictionary root.");
              const required=["ACPI","Booter","DeviceProperties","Kernel","Misc","NVRAM","PlatformInfo","UEFI"];
              required.forEach(key=>{
                const valid=Object.prototype.hasOwnProperty.call(configObject,key) && configObject[key] && typeof configObject[key]==="object";
                add(valid?"pass":"fail","Top-level section: "+key,valid?"Section is present.":"Expected top-level dictionary is missing or has an unexpected type.");
              });

              const references=[
                {key:"ACPI",array:"Add",pathKey:"Path",base:"ACPI",label:"ACPI table"},
                {key:"Kernel",array:"Add",pathKey:"BundlePath",base:"Kexts",label:"Kernel extension"},
                {key:"UEFI",array:"Drivers",pathKey:null,base:"Drivers",label:"UEFI driver"},
                {key:"Misc",array:"Tools",pathKey:"Path",base:"Tools",label:"OpenCore tool"}
              ];
              references.forEach(rule=>{
                const entries=getPlistPath(configObject,[rule.key,rule.array]);
                if (!Array.isArray(entries)) {
                  add("warn",rule.key+"/"+rule.array+" references","The array is missing or has an unexpected type, so its file references were not checked.");
                  return;
                }
                let examined=0, missing=[];
                entries.forEach(entry=>{
                  let enabled=!(entry && typeof entry==="object" && entry.Enabled===false);
                  if (!enabled) return;
                  const value=rule.pathKey ? entry && entry[rule.pathKey] : (typeof entry==="string" ? entry : entry && entry.Path);
                  if (typeof value!=="string" || !value.trim()) return;
                  examined++;
                  const raw=normalizeEfiPath(value);
                  const candidate=raw.startsWith("efi/")?raw:"efi/oc/"+rule.base.toLowerCase()+"/"+raw;
                  if (!has(candidate)) missing.push(value);
                });
                if (missing.length) {
                  add("fail",rule.label+" path references",missing.length+" enabled path(s) were not found: "+missing.slice(0,5).join(", ")+(missing.length>5?" and more.":""));
                } else {
                  add("pass",rule.label+" path references",examined ? "All "+examined+" enabled reference(s) matched files in the selected EFI." : "No enabled path references were found in this array.");
                }
              });
            }
          }
        }
      } catch(error) {
        add("fail","config.plist could not be read","The browser could not read this file. Check file permissions and try again.");
      }
    }
    renderEfiChecks(checks,selectedEfiFiles.length);
    announcer.textContent="EFI inspection completed.";
  } catch(error) {
    add("fail","Inspection stopped",error.message || "Unexpected local inspection error.");
    renderEfiChecks(checks,selectedEfiFiles.length);
  } finally {
    efiRunButton.disabled=false;
    efiRunButton.textContent="Inspect again";
  }
}

efiRunButton.addEventListener("click",inspectEfiFolder);


const troubleForm=document.getElementById("troubleshoot-form");
const troubleOutput=document.getElementById("troubleshoot-results");
const troubleRules=[
  {pattern:/no schema for|failed to parse|plist|config\.plist/i,chapter:"06-config-plist",title:"Review config.plist structure",detail:"Check the exact OpenCore version, plist schema, key types, and whether the setting belongs at the path shown by the configuration reference."},
  {pattern:/exitbs:|still waiting for root device|eb\|log:|kernel panic/i,chapter:"14-troubleshooting",title:"Classify the boot failure",detail:"Record the last visible log line and determine whether the failure occurs before kernel handoff, during storage discovery, or after the kernel starts. Avoid changing several boot settings at once."},
  {pattern:/openruntime|driver.*not found|no driver|opencore\.efi|bootx64/i,chapter:"04-efi-structure",title:"Check the EFI boot structure",detail:"Verify the BOOT and OC layout, the firmware boot path, and the exact UEFI driver paths. Confirm that config.plist references files that actually exist."},
  {pattern:/acpi|ssdt|dsdt/i,chapter:"05-acpi-kexts-drivers",title:"Review ACPI tables and patches",detail:"Check the exact ACPI file names, enabled entries, load order, and hardware-specific purpose. Do not add generic SSDTs without understanding the target."},
  {pattern:/wifi|wi-fi|bluetooth|wireless/i,chapter:"05-acpi-kexts-drivers",title:"Identify the wireless chipset",detail:"Start with the exact PCI device and vendor ID. Support depends on the chipset, macOS release, and the relevant driver project."},
  {pattern:/usb|xhci|port map|usbinjectall/i,chapter:"10-usb-mapping",title:"Review USB mapping",detail:"Separate controller detection from individual port mapping. Check the physical ports and the current mapping method for the macOS release."},
  {pattern:/gpu|graphics|black screen|metal|acceleration|framebuffer/i,chapter:"02-compatibility",title:"Check graphics compatibility",detail:"Identify the exact integrated and discrete GPU models, then verify macOS and Metal support before changing framebuffer or boot arguments."},
  {pattern:/recovery|installer|install macos|install stuck/i,chapter:"08-installation",title:"Review the Recovery and installation stage",detail:"Confirm the installer media, network requirements, target disk, and the last completed installation stage. Preserve a backup before disk changes."}
];
const stageRules={
  firmware:{chapter:"02-compatibility",title:"Firmware and compatibility",detail:"Check UEFI mode, firmware settings relevant to the hardware, and whether the system reaches the boot entry."},
  opencore:{chapter:"04-efi-structure",title:"OpenCore boot stage",detail:"Check the BOOT/OC layout, config.plist, driver entries, and the OpenCore log around the last visible message."},
  installer:{chapter:"08-installation",title:"Recovery and installer stage",detail:"Check Recovery media, storage visibility, network connectivity, and the exact stage where progress stops."},
  firstboot:{chapter:"14-troubleshooting",title:"First boot after installation",detail:"Compare the installed system's boot path with the Recovery boot path and verify the EFI used to start the installed system."},
  postinstall:{chapter:"09-post-installation",title:"Post-installation checks",detail:"Verify hardware one device at a time and record which functions work, partially work, or are unavailable."},
  hardware:{chapter:"02-compatibility",title:"Hardware compatibility",detail:"Identify exact device models and PCI IDs before selecting Kexts or applying configuration changes."},
  unknown:{chapter:"00-read-first",title:"Start with the system state",detail:"Write down the hardware, OpenCore version, macOS target, and exact stage reached before changing anything."}
};
troubleForm.addEventListener("submit",function(event){
  event.preventDefault();
  const stage=document.getElementById("troubleshoot-stage").value;
  const details=document.getElementById("troubleshoot-text").value.trim();
  const matches=[];
  troubleRules.forEach(rule=>{if(details&&rule.pattern.test(details))matches.push(rule)});
  const stageRule=stageRules[stage]||stageRules.unknown;
  if(!matches.some(item=>item.chapter===stageRule.chapter)) matches.push(stageRule);
  troubleOutput.replaceChildren();
  const heading=document.createElement("h3");heading.textContent="Suggested places to investigate";
  troubleOutput.appendChild(heading);
  matches.slice(0,4).forEach(item=>{
    const row=document.createElement("div");row.className="trouble-match";
    const title=document.createElement("b");title.textContent=item.title;
    const detail=document.createElement("p");detail.textContent=item.detail;
    const link=document.createElement("a");link.href="#chapter/"+item.chapter;link.textContent="Open "+(chapters.find(c=>c.id===item.chapter)?.title||"chapter")+" ↗";
    row.append(title,detail,link);troubleOutput.appendChild(row);
  });
  const note=document.createElement("p");note.className="trouble-note";
  note.textContent=details?"Suggestions are based on text matching and the selected stage, not an automatic diagnosis. Review the matching chapter and verify the evidence before changing settings.":"No log text was provided. Showing a general starting point for the selected stage.";
  troubleOutput.appendChild(note);
  troubleOutput.hidden=false;
  announcer.textContent="Troubleshooting suggestions are ready.";
});
troubleForm.addEventListener("reset",()=>{troubleOutput.hidden=true;troubleOutput.replaceChildren()});

menuButton.addEventListener("click", function () {
  const open = !document.body.classList.contains("nav-open");
  setMobileMenu(open);
});
sidebarToggle.addEventListener("click", function () {
  setDocsMenu(!document.body.classList.contains("docs-menu-open"));
});
document.addEventListener("click", function (event) {
  if (document.body.classList.contains("docs-menu-open") && !docsSidebar.contains(event.target) && !sidebarToggle.contains(event.target)) setDocsMenu(false);
  if (document.body.classList.contains("nav-open") && !topNav.contains(event.target) && !menuButton.contains(event.target)) setMobileMenu(false);
});
chapterNav.addEventListener("click", function (event) {
  if (event.target.closest("a")) setDocsMenu(false);
});
docsSearch.addEventListener("input", function () { renderChapterNavigation(docsSearch.value); });

document.addEventListener("keydown", function (event) {
  const shortcut = (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k";
  const slash = event.key === "/" && !/INPUT|TEXTAREA/.test(document.activeElement.tagName);
  if (shortcut || slash) {
    event.preventDefault();
    openSearch();
  }
  if (event.key === "Escape") {
    setMobileMenu(false);
    setDocsMenu(false);
  }
});

function updateThemeButton() {
  const light = document.documentElement.dataset.theme === "light";
  themeButton.textContent = light ? "☾" : "☼";
  themeButton.setAttribute("aria-label", light ? "Switch to dark theme" : "Switch to light theme");
  themeButton.title = light ? "Switch to dark theme" : "Switch to light theme";
}
themeButton.addEventListener("click", function () {
  const root = document.documentElement;
  const next = root.dataset.theme === "light" ? "dark" : "light";
  root.dataset.theme = next;
  try { localStorage.setItem("simple-hackintosh-theme", next); } catch (error) {}
  updateThemeButton();
});

document.getElementById("tools-nav").addEventListener("click",function(event){
  const button=event.target.closest("[data-tool-select]");
  if(!button)return;
  const selected=button.dataset.toolSelect;
  document.querySelectorAll("[data-tool-select]").forEach(item=>{
    const active=item===button;item.classList.toggle("active",active);item.setAttribute("aria-pressed",String(active));
  });
  document.querySelectorAll("[data-tool-panel]").forEach(panel=>{
    panel.hidden=panel.dataset.toolPanel!==selected;panel.classList.toggle("active",panel.dataset.toolPanel===selected);
  });
  window.location.hash="#tools/"+selected;
});
window.addEventListener("hashchange", route);
window.addEventListener("scroll", function () {
  if (progressFrame !== null) return;
  progressFrame = window.requestAnimationFrame(function () {
    updateReadingProgress();
    progressFrame = null;
  });
}, {passive:true});
window.addEventListener("resize", updateReadingProgress);

renderChapterNavigation("");
renderChapterCards("all");
updateProgress();
updateThemeButton();
route();
updateReadingProgress();
