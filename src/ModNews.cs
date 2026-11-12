using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Scenes.MainMenu;
using UnityEngine;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>MOD-NEWS-1 (user request 2026-10-31: "the changes like the News display in the main game"; "the news can
    /// come from the GitHub README"): the mod's news as the first cards of the main menu's own News and Updates panel
    /// (Scenes.MainMenu.NewsAndUpdates), with the mod's picture; the game's own posts follow behind the arrows.
    ///
    /// The cards come from the public repository's README: the section whose heading is "News" (gps_news_section - the
    /// German texts name it "Neuigkeiten"; the other name is the fallback), every heading one level below it is a card,
    /// its text the card's text (Markdown reduced to plain text). A click opens the card's first link, else the card in
    /// the README on GitHub. No README (offline, not reachable, no such section): one built-in card for this version.
    ///
    /// No Harmony patch (the panel's Start and LoadPosts are tiny methods, TINY-DETOUR-1): the menu tick hands over the
    /// MainMenuController, and the panel's private post list is filled by plain reflection - which also works when the
    /// mod's patches failed (banner mode). LoadPosts creates the list before its web request and appends the game's posts
    /// to it, so cards put in front stay in front whether the request is still running, done or failed.</summary>
    internal static class ModNews
    {
        internal const string ReadmeRaw = "https://raw.githubusercontent.com/amaus1209/BigAmbitionsGoingPublicServer/main/README.md";
        internal const string ReadmePage = "https://github.com/amaus1209/BigAmbitionsGoingPublicServer";
        private const float FetchTimeout = 12f;   // the cards wait this long for the README at most
        private const int MaxCards = 8, MaxText = 600;

        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly FieldInfo? _fPanel = typeof(MainMenuController).GetField("newsAndUpdates", Inst);
        private static readonly FieldInfo? _fPosts = typeof(NewsAndUpdates).GetField("_posts", Inst);
        private static readonly FieldInfo? _fCount = typeof(NewsAndUpdates).GetField("numberOfPostsToLoad", Inst);
        private static readonly FieldInfo? _fImage = typeof(NewsAndUpdates).GetField("displayImage", Inst);
        private static readonly MethodInfo? _mSetPost = typeof(NewsAndUpdates).GetMethod("SetPost", Inst);

        private static volatile string? _readme;   // null = not fetched yet; "" = fetch failed (written by the download task)
        private static float _giveUpAt = -1f;       // <0 = fetch not started
        private static int _doneFor;           // instance id of the panel the cards went into (each menu visit builds a new one)
        private static Texture2D? _picture;    // the mod's picture in the panel's shape, kept for every visit
        private static bool _warned;

        /// <summary>Once per game start, from the first menu tick: the README, in the background - the same way the
        /// translation tables come from this repository (MOD-TRANSLATIONS-2). A README that arrives after the cards gave
        /// up waiting shows from the next menu visit.</summary>
        internal static void StartFetch()
        {
            if (_giveUpAt >= 0f) return;
            _giveUpAt = Time.unscaledTime + FetchTimeout;
            Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    string md;
                    using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                        md = wc.DownloadString(ReadmeRaw + "?t=" + System.DateTime.UtcNow.Ticks) ?? "";
                    _readme = md;
                    Plugin.Logger.LogInfo($"[News] README loaded ({md.Length} characters).");
                }
                catch (System.Exception ex)
                {
                    _readme = "";
                    Plugin.Logger.LogInfo($"[News] README not loaded ({ex.Message}) - the built-in card is shown.");
                }
            });
        }

        /// <summary>Twice a second while the main menu is up (MPCanvasUI.TickMenuExtras).</summary>
        internal static void Inject(MainMenuController mmc)
        {
            int id = 0;
            try
            {
                if (_readme == null && Time.unscaledTime < _giveUpAt) return;   // the README is still on its way
                if (_fPanel?.GetValue(mmc) is not NewsAndUpdates panel || panel == null) return;
                id = panel.GetInstanceID();
                if (id == _doneFor) return;
                if (_fPosts?.GetValue(panel) is not List<Post> posts) return;   // its Start has not run yet
                _doneFor = id;   // once per panel, also when something below fails
                var cards = Cards(panel, out string from);
                posts.InsertRange(0, cards);
                // LoadPosts stops at numberOfPostsToLoad: keep the room for the game's own posts
                if (_fCount != null) _fCount.SetValue(panel, (int)_fCount.GetValue(panel) + cards.Count);
                _mSetPost?.Invoke(panel, new object[] { 0 });
                Plugin.Logger.LogInfo($"[News] {cards.Count} card(s) {from} put in front of the game's news (MOD-NEWS-1).");
            }
            catch (System.Exception ex)
            {
                if (id != 0) _doneFor = id;
                if (!_warned) { _warned = true; Plugin.Logger.LogWarning($"[News] cards not shown: {ex.Message}"); }
            }
        }

        private static List<Post> Cards(NewsAndUpdates panel, out string from)
        {
            var pic = Picture(panel);
            var list = new List<Post>();
            string want = Loc.T("gps_news_section", "News");
            string other = want == "News" ? "Neuigkeiten" : "News";
            var found = Parse(_readme ?? "", want);
            if (found.Count == 0) found = Parse(_readme ?? "", other);
            foreach (var (title, text, url) in found)
                list.Add(new Post { title = title, description = text, url = url, steamUrl = url, displayImage = pic });
            if (list.Count > 0) { from = "from the GitHub README"; return list; }
            from = "built in (no news in the README)";
            list.Add(new Post
            {
                title = Loc.T("gps_news_builtin_title", "{0} {1}", MyPluginInfo.SHORT_NAME, MyPluginInfo.PLUGIN_VERSION),
                description = Loc.T("gps_news_builtin_text", "The mod's news is in the README on GitHub - it could not be loaded just now."),
                url = ReadmePage, steamUrl = ReadmePage, displayImage = pic,
            });
            return list;
        }

        private static readonly Regex Heading = new Regex(@"^(#{1,6})\s+(.+?)\s*#*\s*$");
        private static readonly Regex Link = new Regex(@"\[([^\]]*)\]\(([^)\s]+)[^)]*\)");
        private static readonly Regex Image = new Regex(@"!\[[^\]]*\]\([^)]*\)");
        private static readonly Regex HtmlTag = new Regex(@"</?(img|br|a|p|b|i|em|strong|div|span|sup|sub|details|summary)\b[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex AngleBrackets = new Regex(@"<([^<>]*)>");

        /// <summary>The cards of the README section named <paramref name="section"/>: every heading one level below it,
        /// up to the next heading of the section's level or higher.</summary>
        internal static List<(string title, string text, string url)> Parse(string md, string section)
        {
            var cards = new List<(string, string, string)>();
            if (string.IsNullOrEmpty(md)) return cards;
            var lines = md.Replace("\r", "").Split('\n');
            int level = 0;
            string? title = null, anchor = null;
            var body = new StringBuilder();
            var anchors = new Dictionary<string, int>();   // GitHub numbers a repeated heading's anchor: x, x-1, x-2 ...
            void Flush()
            {
                if (title == null || cards.Count >= MaxCards) return;
                var (text, url) = Plain(body.ToString());
                cards.Add((title, text, url.Length > 0 ? url : ReadmePage + "#" + anchor));
            }
            bool inCode = false, inComment = false;
            foreach (var raw in lines)
            {
                string t = raw.Trim();
                if (inComment) { if (t.Contains("-->")) inComment = false; continue; }
                if (t.StartsWith("<!--")) { inComment = !t.Contains("-->"); continue; }   // notes for the README's editor
                if (t.StartsWith("```")) { inCode = !inCode; continue; }
                if (inCode) continue;
                if (level > 0 && t == "---") break;                               // a rule ends the section too
                var h = Heading.Match(raw);
                if (h.Success)
                {
                    int l = h.Groups[1].Value.Length;
                    string name = Plain(h.Groups[2].Value).text;
                    string slug = Slug(name);
                    anchors.TryGetValue(slug, out int seen);
                    anchors[slug] = seen + 1;
                    if (seen > 0) slug += "-" + seen;
                    if (level == 0)
                    {
                        if (string.Equals(name, section, System.StringComparison.OrdinalIgnoreCase)) level = l;
                        continue;
                    }
                    if (l <= level) break;                                    // the next section: done
                    if (l == level + 1) { Flush(); title = name; anchor = slug; body.Clear(); continue; }
                    if (title != null) body.Append(name).Append("\n\n");      // a heading inside a card: its own line
                    continue;
                }
                if (level > 0 && title != null) body.Append(raw).Append('\n');
            }
            if (level > 0) Flush();
            return cards;
        }

        /// <summary>Markdown to the panel's plain text: links keep their text (the first one's address is returned), no
        /// pictures, HTML tags, emphasis or code marks; list items become bullets; paragraphs stay apart.</summary>
        private static (string text, string url) Plain(string md)
        {
            string s = Image.Replace(md, "");
            var m = Link.Match(s);
            string url = m.Success ? m.Groups[2].Value : "";
            s = Link.Replace(s, "$1");
            s = HtmlTag.Replace(s, "");
            s = s.Replace("**", "").Replace("__", "").Replace("`", "");
            var sb = new StringBuilder();
            foreach (var raw in s.Split('\n'))
            {
                string line = raw.Trim();
                while (line.StartsWith(">")) line = line.Substring(1).TrimStart();   // a quote
                // the panel is TextMeshPro: no tags out of the README (only '<' opens one - "->" stays as it is)
                line = AngleBrackets.Replace(line, "«$1»").Replace("<", "«");
                line = Regex.Replace(line, " {2,}", " ");                                // gaps left by removed pictures and tags
                if (line.Length == 0 || line == "---" || line == "***") { if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n'); continue; }
                bool bullet = line.StartsWith("- ") || line.StartsWith("* ");
                if (bullet) line = "• " + line.Substring(2);
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append(bullet ? '\n' : ' ');
                sb.Append(line);
            }
            string text = sb.ToString().Trim();
            if (text.Length > MaxText) text = text.Substring(0, MaxText - 1).TrimEnd() + "…";
            if (url.Length > 0 && !url.StartsWith("http")) url = ReadmePage + "/blob/main/" + url.TrimStart('/');   // a link into the repository
            return (text, url);
        }

        /// <summary>GitHub's anchor for a heading: lower case, spaces to '-', no punctuation.</summary>
        private static string Slug(string title)
        {
            var sb = new StringBuilder();
            foreach (char c in title.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
                else if (c == ' ') sb.Append('-');
            }
            return sb.ToString();
        }

        /// <summary>preview.png (square) on a background in the panel's own shape, so the panel does not stretch it.
        /// null = the game's default picture.</summary>
        private static Texture2D? Picture(NewsAndUpdates panel)
        {
            if (_picture != null) return _picture;
            try
            {
                string path = Path.Combine(MPConfig.ModRootPath, "preview.png");
                if (!File.Exists(path)) return null;
                var logo = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!logo.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(logo); return null; }
                float aspect = 16f / 9f;
                try
                {
                    var r = (_fImage?.GetValue(panel) as Image)?.rectTransform.rect ?? default;
                    if (r.width > 1f && r.height > 1f) aspect = r.width / r.height;
                }
                catch { }
                int h = logo.height, lw = logo.width;
                int w = Mathf.Max(lw, Mathf.RoundToInt(h * aspect));
                var src = logo.GetPixels32();
                // background = the picture's own edge colour, darkened
                long sr = 0, sg = 0, sb = 0;
                for (int y = 0; y < h; y++) { var a = src[y * lw]; var b = src[y * lw + lw - 1]; sr += a.r + b.r; sg += a.g + b.g; sb += a.b + b.b; }
                int n = 2 * h;
                var fill = new Color32((byte)(sr / n * 6 / 10), (byte)(sg / n * 6 / 10), (byte)(sb / n * 6 / 10), 255);
                var px = new Color32[w * h];
                for (int i = 0; i < px.Length; i++) px[i] = fill;
                int x0 = (w - lw) / 2;
                for (int y = 0; y < h; y++) System.Array.Copy(src, y * lw, px, y * w + x0, lw);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply(false, true);
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;   // a scene change's asset sweep must not take it
                UnityEngine.Object.Destroy(logo);
                return _picture = tex;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"[News] picture: {ex.Message}");
                return null;
            }
        }
    }
}
