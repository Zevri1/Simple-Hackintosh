const chapters = [
  {id:"00-read-first",title:"Read this first",group:"START HERE"},
  {id:"01-hardware-inventory",title:"Hardware inventory",group:"START HERE"},
  {id:"02-compatibility",title:"Compatibility check",group:"START HERE"},
  {id:"03-usb-installer-windows",title:"Windows USB installer",group:"BUILD YOUR EFI"},
  {id:"04-efi-structure",title:"EFI structure",group:"BUILD YOUR EFI"},
  {id:"05-acpi-kexts-drivers",title:"ACPI, Kexts & drivers",group:"BUILD YOUR EFI"},
  {id:"06-config-plist",title:"Configure config.plist",group:"BUILD YOUR EFI"},
  {id:"07-validation-debugging",title:"Validation & debugging",group:"BUILD YOUR EFI"},
  {id:"08-installation",title:"Install macOS",group:"INSTALL & VERIFY"},
  {id:"09-post-installation",title:"Post-installation",group:"INSTALL & VERIFY"},
  {id:"10-usb-mapping",title:"USB mapping",group:"INSTALL & VERIFY"},
  {id:"11-macos-tahoe-26",title:"macOS Tahoe 26",group:"INSTALL & VERIFY"},
  {id:"12-dual-boot",title:"Dual boot safety",group:"MAINTAIN & LEARN"},
  {id:"13-maintenance",title:"Maintenance & updates",group:"MAINTAIN & LEARN"},
  {id:"14-troubleshooting",title:"Troubleshooting",group:"MAINTAIN & LEARN"},
  {id:"15-glossary",title:"Glossary",group:"MAINTAIN & LEARN"},
  {id:"16-visual-walkthrough",title:"Visual walkthrough",group:"MAINTAIN & LEARN"}
];

const homeView = document.getElementById("home-view");
const guideView = document.getElementById("guide-view");
const article = document.getElementById("doc-article");
const tocLinks = document.getElementById("toc-links");
const pagination = document.getElementById("chapter-pagination");
const sidebar = document.getElementById("sidebar");
const searchInput = document.getElementById("doc-search");
const searchResults = document.getElementById("search-results");
const chapterNav = document.getElementById("chapter-nav");
const menuButton = document.getElementById("mobile-menu");
let searchIndex = [];
let searchLoaded = false;
let activeChapter = null;
let loadSequence = 0;

function escapeHTML(value) {
  return String(value).replace(/[&<>"']/g, function (ch) {
    return {"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[ch];
  });
}
function safeHref(value) {
  try {
    const decoded = String(value).replace(/&amp;/g, "&").trim();
    if (/^(https?:|mailto:|#|\.\.?\/)/i.test(decoded)) return escapeHTML(decoded);
  } catch (e) {}
  return "#";
}
function slugify(value) {
  return value.toLowerCase().trim().replace(/[^a-z0-9\s-]/g, "").replace(/\s+/g, "-").replace(/-+/g, "-");
}
function inlineMarkdown(input) {
  let text = escapeHTML(input);
  const codeTokens = [];
  text = text.replace(/`([^`]+)`/g, function (_, code) {
    const token = "%%CODE" + codeTokens.length + "%%";
    codeTokens.push("<code>" + code + "</code>");
    return token;
  });
  text = text.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, function (_, alt, url) {
    return '<img loading="lazy" alt="' + alt + '" src="' + safeHref(url) + '">';
  });
  text = text.replace(/\[([^\]]+)\]\(([^)]+)\)/g, function (_, label, url) {
    const href = safeHref(url);
    const external = /^https?:/i.test(url);
    return '<a href="' + href + '"' + (external ? ' target="_blank" rel="noopener noreferrer"' : "") + ">" + label + "</a>";
  });
  text = text.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
  text = text.replace(/\*([^*]+)\*/g, "<em>$1</em>");
  text = text.replace(/~~(.+?)~~/g, "<del>$1</del>");
  codeTokens.forEach(function (html, i) {
    text = text.replace("%%CODE" + i + "%%", html);
  });
  return text;
}
function parseTableRow(line) {
  return line.trim().replace(/^\|/, "").replace(/\|$/, "").split("|").map(function (cell) { return cell.trim(); });
}
function renderMarkdown(markdown) {
  const lines = markdown.replace(/\r/g, "").split("\n");
  let html = "";
  let paragraph = [];
  let listType = null;
  let inCode = false;
  let codeLines = [];
  let inQuote = false;
  let quoteLines = [];
  let i = 0;
  function flushParagraph() {
    if (paragraph.length) {
      html += "<p>" + inlineMarkdown(paragraph.join(" ")) + "</p>";
      paragraph = [];
    }
  }
  function closeList() {
    if (listType) { html += "</" + listType + ">"; listType = null; }
  }
  function flushQuote() {
    if (inQuote) {
      html += "<blockquote>" + quoteLines.map(function (line) { return "<p>" + inlineMarkdown(line.replace(/^> ?/, "")) + "</p>"; }).join("") + "</blockquote>";
      quoteLines = [];
      inQuote = false;
    }
  }
  while (i < lines.length) {
    const line = lines[i];
    if (line.trim().startsWith("```")) {
      flushParagraph(); closeList(); flushQuote();
      if (!inCode) { inCode = true; codeLines = []; }
      else { html += "<pre><code>" + escapeHTML(codeLines.join("\n")) + "</code></pre>"; inCode = false; }
      i++; continue;
    }
    if (inCode) { codeLines.push(line); i++; continue; }
    if (/^>/.test(line)) {
      flushParagraph(); closeList(); inQuote = true; quoteLines.push(line); i++; continue;
    } else if (inQuote) { flushQuote(); }
    if (!line.trim()) { flushParagraph(); closeList(); i++; continue; }
    if (/^\s*---+\s*$/.test(line)) { flushParagraph(); closeList(); html += "<hr>"; i++; continue; }
    const heading = line.match(/^(#{1,4})\s+(.+)$/);
    if (heading) {
      flushParagraph(); closeList();
      const level = heading[1].length;
      const title = heading[2].replace(/\s+#+\s*$/, "");
      const id = slugify(title);
      html += "<h" + level + ' id="' + id + '">' + inlineMarkdown(title) + "</h" + level + ">";
      i++; continue;
    }
    if (i + 1 < lines.length && /^\s*\|?\s*:?-{3,}/.test(lines[i+1])) {
      flushParagraph(); closeList();
      const headers = parseTableRow(line);
      i += 2;
      let rows = "";
      while (i < lines.length && /^\s*\|/.test(lines[i])) {
        const cells = parseTableRow(lines[i]);
        rows += "<tr>" + headers.map(function (_, idx) {
          return "<td>" + inlineMarkdown(cells[idx] || "") + "</td>";
        }).join("") + "</tr>";
        i++;
      }
      html += "<table><thead><tr>" + headers.map(function (cell) { return "<th>" + inlineMarkdown(cell) + "</th>"; }).join("") + "</tr></thead><tbody>" + rows + "</tbody></table>";
      continue;
    }
    const unordered = line.match(/^\s*[-*+]\s+(.+)$/);
    const ordered = line.match(/^\s*\d+\.\s+(.+)$/);
    if (unordered || ordered) {
      flushParagraph();
      const needed = unordered ? "ul" : "ol";
      if (listType !== needed) { closeList(); listType = needed; html += "<" + listType + ">"; }
      let item = (unordered || ordered)[1];
      item = item.replace(/^\[ \]\s+/, "☐ ").replace(/^\[x\]\s+/i, "☑ ");
      html += "<li>" + inlineMarkdown(item) + "</li>";
      i++; continue;
    }
    closeList();
    paragraph.push(line.trim());
    i++;
  }
  flushParagraph(); closeList(); flushQuote();
  if (inCode) html += "<pre><code>" + escapeHTML(codeLines.join("\n")) + "</code></pre>";
  return html;
}
function extractHeadings(markdown) {
  return markdown.split("\n").map(function (line) {
    const match = line.match(/^(#{2,3})\s+(.+)$/);
    if (!match) return null;
    const title = match[2].replace(/\s+#+\s*$/, "");
    return {title:title, id:slugify(title), level:match[1].length};
  }).filter(Boolean);
}
function setActiveNavigation(id) {
  document.querySelectorAll(".chapter-link").forEach(function (link) {
    const active = link.getAttribute("data-chapter") === id;
    link.classList.toggle("active", active);
    if (active) link.setAttribute("aria-current", "page");
    else link.removeAttribute("aria-current");
  });
}
function setTopLinks(isHome) {
  document.querySelector("[data-home-link]").classList.toggle("active", isHome);
  document.querySelector("[data-guide-link]").classList.toggle("active", !isHome);
}
function renderToc(headings) {
  tocLinks.innerHTML = headings.map(function (item) {
    return '<a class="' + (item.level === 3 ? "toc-sub" : "") + '" href="#' + item.id + '">' + escapeHTML(item.title) + "</a>";
  }).join("");
}
function renderPagination(index) {
  const previous = chapters[index - 1];
  const next = chapters[index + 1];
  pagination.innerHTML = "";
  if (previous) {
    const a = document.createElement("a");
    a.className = "page-nav";
    a.href = "#chapter/" + previous.id;
    a.innerHTML = "<small>← PREVIOUS CHAPTER</small><b>" + escapeHTML(previous.title) + "</b>";
    pagination.appendChild(a);
  }
  if (next) {
    const a = document.createElement("a");
    a.className = "page-nav next";
    a.href = "#chapter/" + next.id;
    a.innerHTML = "<small>NEXT CHAPTER →</small><b>" + escapeHTML(next.title) + "</b>";
    pagination.appendChild(a);
  }
}
async function openChapter(id) {
  const chapter = chapters.find(function (item) { return item.id === id; });
  if (!chapter) { showHome(); return; }
  const requestId = ++loadSequence;
  activeChapter = id;
  homeView.hidden = true;
  guideView.hidden = false;
  setTopLinks(false);
  setActiveNavigation(id);
  document.body.classList.remove("menu-open");
  menuButton.setAttribute("aria-expanded", "false");
  article.innerHTML = '<div class="loading-state"><div class="loading-spinner"></div><p>Loading chapter…</p></div>';
  tocLinks.innerHTML = "";
  pagination.innerHTML = "";
  try {
    const response = await fetch("docs/" + id + ".md", {cache:"no-cache"});
    if (!response.ok) throw new Error("Could not load this chapter (HTTP " + response.status + ").");
    const markdown = await response.text();
    if (requestId !== loadSequence) return;
    article.innerHTML = renderMarkdown(markdown);
    const firstHeading = article.querySelector("h1");
    if (firstHeading) {
      firstHeading.setAttribute("tabindex", "-1");
      firstHeading.focus({preventScroll:true});
    }
    renderToc(extractHeadings(markdown));
    renderPagination(chapters.findIndex(function (item) { return item.id === id; }));
    document.title = chapter.title + " — Simple Hackintosh";
    window.scrollTo({top:0,behavior:"smooth"});
  } catch (error) {
    if (requestId !== loadSequence) return;
    article.innerHTML = '<div class="error-state"><h2>Chapter could not be loaded</h2><p>' + escapeHTML(error.message) + '</p><p>Open the source file directly on <a href="https://github.com/kiwewe09-aa/Simple-Hackintosh/blob/main/docs/' + id + '.md" target="_blank" rel="noopener noreferrer">GitHub ↗</a>.</p></div>';
  }
}
function showHome() {
  loadSequence++;
  activeChapter = null;
  homeView.hidden = false;
  guideView.hidden = true;
  setActiveNavigation("");
  setTopLinks(true);
  document.title = "Simple Hackintosh — OpenCore Learning Guide";
  document.body.classList.remove("menu-open");
  menuButton.setAttribute("aria-expanded", "false");
}
function route() {
  const hash = window.location.hash || "#home";
  if (hash.startsWith("#chapter/")) {
    openChapter(hash.slice("#chapter/".length).split("/")[0]);
  } else if (hash === "#guide") {
    openChapter("00-read-first");
  } else {
    showHome();
  }
}
async function buildSearchIndex() {
  if (searchLoaded) return searchIndex;
  searchLoaded = true;
  const results = await Promise.allSettled(chapters.map(async function (chapter) {
    const response = await fetch("docs/" + chapter.id + ".md");
    if (!response.ok) throw new Error("fetch failed");
    const markdown = await response.text();
    return {id:chapter.id,title:chapter.title,text:markdown.replace(/[#>*_|`\[\]()!-]/g, " ").replace(/\s+/g, " ").toLowerCase()};
  }));
  searchIndex = results.filter(function (r) { return r.status === "fulfilled"; }).map(function (r) { return r.value; });
  return searchIndex;
}
function runSearch(query) {
  const q = query.trim().toLowerCase();
  if (!q) { searchResults.hidden = true; searchResults.innerHTML = ""; return; }
  searchResults.hidden = false;
  searchResults.innerHTML = '<div class="search-result">Searching chapters…</div>';
  buildSearchIndex().then(function (index) {
    if (searchInput.value.trim().toLowerCase() !== q) return;
    const found = index.filter(function (item) { return item.title.toLowerCase().includes(q) || item.text.includes(q); }).slice(0,7);
    searchResults.innerHTML = found.length ? found.map(function (item) {
      return '<a class="search-result" href="#chapter/' + item.id + '">' + escapeHTML(item.title) + '<small>Open chapter ↗</small></a>';
    }).join("") : '<div class="search-result">No matching chapter found.<small>Try a hardware or OpenCore term.</small></div>';
  });
}
searchInput.addEventListener("input", function () { runSearch(searchInput.value); });
searchInput.addEventListener("keydown", function (event) {
  if (event.key === "Escape") { searchInput.value = ""; runSearch(""); searchInput.blur(); }
  if (event.key === "Enter") {
    const first = searchResults.querySelector("a");
    if (first) window.location.hash = first.getAttribute("href");
  }
});
document.addEventListener("keydown", function (event) {
  if (event.key === "/" && !/INPUT|TEXTAREA/.test(document.activeElement.tagName)) {
    event.preventDefault(); searchInput.focus();
  }
});
menuButton.addEventListener("click", function () {
  const open = !document.body.classList.contains("menu-open");
  document.body.classList.toggle("menu-open", open);
  menuButton.setAttribute("aria-expanded", String(open));
});
document.addEventListener("click", function (event) {
  if (document.body.classList.contains("menu-open") && !sidebar.contains(event.target) && !menuButton.contains(event.target)) {
    document.body.classList.remove("menu-open");
    menuButton.setAttribute("aria-expanded", "false");
  }
});
document.getElementById("theme-toggle").addEventListener("click", function () {
  const root = document.documentElement;
  const next = root.getAttribute("data-theme") === "light" ? "dark" : "light";
  root.setAttribute("data-theme", next);
  try { localStorage.setItem("simple-hackintosh-theme", next); } catch (e) {}
});
try {
  const savedTheme = localStorage.getItem("simple-hackintosh-theme");
  if (savedTheme === "light" || savedTheme === "dark") document.documentElement.setAttribute("data-theme", savedTheme);
} catch (e) {}
window.addEventListener("hashchange", route);
route();
