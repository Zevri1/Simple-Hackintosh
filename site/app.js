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

function setTopNavigation(isHome) {
  document.querySelector("[data-home-link]").classList.toggle("active", isHome);
  document.querySelector("[data-guide-link]").classList.toggle("active", !isHome);
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
  setTopNavigation(true);
  setDocsMenu(false);
  document.title = "Simple Hackintosh — OpenCore Field Guide";
  document.body.classList.remove("nav-open");
  menuButton.setAttribute("aria-expanded", "false");
}

async function openChapter(id, anchorId) {
  const chapter = chapters.find(item => item.id === id);
  if (!chapter) { showHome(); return; }
  const requestId = ++loadSequence;
  activeChapter = id;
  homeView.hidden = true;
  docsView.hidden = false;
  setTopNavigation(false);
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

function route() {
  const hash = window.location.hash || "#home";
  if (hash.startsWith("#chapter/")) {
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
