
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Linq;
using UMC.Web;
using UMC.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;

namespace UMC.Proxy
{
    public class HttpProxy
    {
        //class LogWriter : System.IO.TextWriter
        //{
        //    public LogWriter(String filename)
        //    {
        //        this.file = filename;
        //    }
        //    String file;
        //    System.IO.TextWriter writer;
        //    public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        //    public override void Write(char value)
        //    {
        //        if (writer == null)
        //        {
        //            if (String.IsNullOrEmpty(file) == false)
        //            {
        //                if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(file)))
        //                {
        //                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
        //                }
        //                FileStream stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

        //                writer = new System.IO.StreamWriter(stream);

        //            }
        //        }
        //        if (writer != null)
        //        {
        //            writer.Write(value);
        //        }
        //    }

        //    public override void Close()
        //    {
        //        base.Close();
        //        if (writer != null)
        //        {
        //            writer.Close();
        //        }
        //    }
        //}
        public Uri Domain
        {
            get;
            private set;
        }
        public SiteConfig Site
        {
            get;
            private set;
        }
        public UMC.Proxy.Entities.Cookie SiteCookie
        {
            get;
            private set;
        }

        public String WebResource
        {

            get;
            private set;
        }
        public System.IO.StringWriter Loger
        {

            get;
            private set;
        }
        public String Host
        {
            get;
            private set;
        }
        public bool? IsChangeUser
        {
            get;
            set;
        }
        String sourceUP;

        public string Password
        {
            get;
            private set;
        }
        private HttpProxy(HttpProxy proxy, SiteConfig siteConfig)
        {
            var user = siteConfig.Site.Account;

            this.Site = siteConfig;
            this.Loger = proxy.Loger;
            this.IsDebug = proxy.IsDebug;
            this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(siteConfig.Root, user));

            this.SiteCookie = new Entities.Cookie
            {
                Account = user,
                user_id = UMC.Data.Utility.Guid(user, true),
                Domain = this.Site.Root,
                IndexValue = 0
            };
            this.IsChangeUser = false;
            this.Context = proxy.Context;
            this.WebResource = proxy.WebResource;
            this.Domain = new Uri(siteConfig.Domains[0]);
            this.RawUrl = proxy.RawUrl;
            this.siteProxy = proxy.siteProxy;
            this.Host = proxy.Host;
            this.Cookies = new NetCookieContainer();


        }
        void SetCookie(Cookie cookie)
        {
            if (this.Site.Site.AuthType > WebAuthType.All)
            {
                if (String.IsNullOrEmpty(cookie.Path))
                {
                    this.Context.AddHeader("Set-Cookie", $"{cookie.Name}={cookie.Value}");
                }
                else
                {
                    foreach (var name in this.OutputCookies)
                    {
                        if (String.Equals(cookie.Name, name))
                        {
                            if (String.Equals(this.Context.Cookies[name], cookie.Value) == false)
                            {
                                this.Context.AppendCookie(name, cookie.Value, cookie.Path);

                            }
                        }
                        else if (String.Equals("*", name))
                        {
                            this.Context.AppendCookie(cookie.Name, cookie.Value, cookie.Path);
                            break;
                        }
                    }
                }
            }

        }
        private string[] OutputCookies = new string[0];

        const string DeviceIndex = "DeviceIndex";

        private bool IsCDN
        {
            get; set;
        }

        public static Uri WeightUri(SiteConfig site, UMC.Net.NetContext context)
        {

            if (site.WeightTotal > 0)
            {
                var value = 0;
                if (site.WeightTotal > 1)
                {
                    switch (site.Site.SLB ?? 0)
                    {
                        default:
                        case 0:
                            var r = new Random();
                            value = r.Next(0, site.WeightTotal);
                            break;
                        case 1:
                            value = Math.Abs(UMC.Data.Utility.IntParse(UMC.Data.Utility.Guid(context.UserHostAddress, true).Value)) % site.WeightTotal;
                            break;
                        case 2:
                            var tk = context.Token.Id;
                            if (tk.HasValue)
                            {
                                value = Math.Abs(UMC.Data.Utility.IntParse(tk.Value)) % site.WeightTotal;

                            }
                            else
                            {
                                var cookie = context.Headers.Get("Cookie");
                                if (String.IsNullOrEmpty(cookie) == false)
                                {
                                    var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                                    value = Math.Abs(UMC.Data.Utility.IntParse(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(cookie)))) % site.WeightTotal;
                                }
                                else
                                {
                                    value = 0;
                                }
                            };
                            break;
                    }
                }

                var qty = 0;
                for (var i = 0; i < site.Weights.Length; i++)
                {
                    qty += site.Weights[i];
                    if (value < qty)
                    {
                        return new Uri(site.Domains[i]);

                    }
                }

            }
            return new Uri(site.Domains[0]);
        }

        public HttpProxy(SiteConfig site, UMC.Net.NetContext context)
        {
            this.Site = site;
            this.IsDebug = site.Site.IsDebug == true;

            this.Loger = new StringWriter();

            this.Context = context;
            this.User = context.Token.Identity();

            this.WebResource = $"/__CDN/{this.Site.Root}";

            this.RawUrl = this.Context.RawUrl;
            if (this.RawUrl.StartsWith(this.WebResource))
            {
                var sUrlIndex = this.RawUrl.IndexOf('/', this.WebResource.Length + 2);
                if (sUrlIndex > 0)
                {
                    this.RawUrl = this.RawUrl.Substring(sUrlIndex);
                    this.StaticModel = 0;
                    this.IsCDN = true;

                }
                else
                {
                    StaticModel = CheckStaticPage(this.Context.Url.AbsolutePath);
                }
            }
            else
            {

                StaticModel = CheckStaticPage(this.Context.Url.AbsolutePath);
            }


            if (_Proxy.Providers.ContainsKey(site.Root))
            {
                this.siteProxy = UMC.Data.Reflection.CreateObject(_Proxy, site.Root) as UMC.Proxy.SiteProxy;
            }


            this.Cookies = new NetCookieContainer(this.SetCookie);
            if (StaticModel != 0)
            {

                //var user = context.Token.Identity(); // UMC.Security.Identity.Current;

                var deviceIndex = UMC.Data.Utility.IntParse(context.Cookies[DeviceIndex], 0);


                if (this.Site.Site.AuthType > WebAuthType.All)
                {
                    bool isNew = false;
                    if (context.RawUrl.StartsWith("/?$=") || context.RawUrl.StartsWith("/?%24="))
                    {
                        isNew = String.Equals("New", context.QueryString.Get("$"));
                        if (isNew == false)
                        {
                            if (context.UrlReferrer != null)
                            {
                                isNew = String.Equals(context.UrlReferrer.PathAndQuery, "/?$=New") || String.Equals(context.UrlReferrer.PathAndQuery, "/?%24=New");
                            }
                        }
                    }
                    if (isNew)
                    {
                        var scookies = DataFactory.Instance().Cookies(this.Site.Root, User.Id.Value).OrderBy(r => r.IndexValue).ToList();

                        foreach (var sc in scookies)
                        {
                            if (String.IsNullOrEmpty(sc.Account))
                            {
                                this.SiteCookie = sc;

                                break;
                            }
                        }
                    }
                    if (this.SiteCookie == null && User.IsAuthenticated)
                    {
                        if (site.Site.UserModel == Entities.UserModel.Bridge && deviceIndex == 0)
                        {
                            this.SiteCookie = new Entities.Cookie { Domain = Site.Root, user_id = User.Id.Value, IndexValue = 0 };
                        }
                        else
                        {
                            this.SiteCookie = DataFactory.Instance().Cookie(this.Site.Root, User.Id.Value, deviceIndex);
                        }
                        if (this.SiteCookie == null && deviceIndex != 0)
                        {
                            this.SiteCookie = DataFactory.Instance().Cookie(this.Site.Root, User.Id.Value, 0);

                        }
                    }
                    if (this.SiteCookie == null)
                    {
                        this.SiteCookie = new Entities.Cookie { Domain = Site.Root, user_id = User.Id.Value, IndexValue = 0 };

                    }
                }

                this.Domain = WeightUri(site, context);
                if (site.Test.Count > 0 && User.IsAuthenticated)
                {
                    var me = site.Test.GetEnumerator();
                    while (me.MoveNext())
                    {
                        if (me.Current.Value.Any(r => String.Equals(r, User.Name, StringComparison.CurrentCultureIgnoreCase))) //.Contains(user.Name))
                        {
                            this.Domain = new Uri(me.Current.Key);

                            break;
                        }
                    }
                }

                this.RawUrl = ReplaceRawUrl(this.RawUrl);

                if (this.SiteCookie != null && String.IsNullOrEmpty(this.SiteCookie.Cookies) == false)
                {
                    var cookies = UMC.Data.JSON.Deserialize<WebMeta[]>(this.SiteCookie.Cookies);
                    if (cookies != null)
                    {

                        var cs = new List<String>();

                        if (this.siteProxy != null)
                        {
                            cs.AddRange(this.siteProxy.OutputCookies);
                        }

                        cs.AddRange(this.Site.OutputCookies);

                        OutputCookies = cs.ToArray();
                        var nowTime = UMC.Data.Utility.TimeSpan();
                        var checkClients = new List<String>();
                        foreach (var v in cookies)
                        {
                            var name = v["name"] as string;
                            var value = v["value"] as string;
                            var path = (v["path"] as string) ?? "/";
                            var domain = (v["domain"] as string) ?? this.Domain.Host;
                            var cookie = new System.Net.Cookie(name, value, path, domain);
                            if (v.ContainsKey("expires"))
                            {
                                var time = UMC.Data.Utility.IntParse(v["expires"] as string, 0);
                                if (time < nowTime)
                                {
                                    continue;
                                }
                                cookie.Expires = UMC.Data.Utility.TimeSpan(time);
                            }

                            cs.RemoveAll(r => String.Equals(cookie.Name, r));
                            checkClients.Add(name);
                            this.Cookies.Add(cookie);
                        }
                        if (cs.Contains("*"))
                        {
                            this.InitClientCookie(checkClients);
                        }
                        else
                        {
                            foreach (var k in cs)
                            {
                                var cvalue = this.Context.Cookies[k];
                                if (String.IsNullOrEmpty(cvalue) == false)
                                {
                                    this.Cookies.Add(new System.Net.Cookie(k, cvalue, "/", this.Domain.Host));
                                }
                            }
                        }
                    }
                }
                else if (site.Site.AuthType == WebAuthType.All)
                {

                    InitClientCookie(new List<string>());
                }

            }
            else if (site.Domains.Length > 0)
            {

                this.Domain = WeightUri(site, context);

                if (this.IsCDN)
                {
                    InitClientCookie(new List<string>());
                }
            }
            if (String.IsNullOrEmpty(this.Site.Site.Host) == false && this.Domain != null)
            {
                var port = this.Domain.Port;
                var host = this.Site.Site.Host;
                switch (port)
                {
                    case 80:
                    case 443:

                        if (String.Equals(host, "*"))
                        {
                            this.Host = this.Context.Url.Authority;
                        }
                        else
                        {
                            this.Host = host;
                        }
                        break;
                    default:
                        if (String.Equals(host, "*"))
                        {
                            this.Host = this.Context.Url.Authority;
                        }
                        else
                        {
                            this.Host = $"{host}:{port}";
                        }
                        break;
                }

            }

            if (this.SiteCookie == null)
            {
                this.SiteCookie = new Entities.Cookie { Domain = Site.Root, user_id = context.Token.UId.Value, IndexValue = 0 };

            }
        }
        void InitClientCookie(List<String> cookis)
        {
            var ms = this.Context.Cookies;
            for (var i = 0; i < ms.Count; i++)
            {
                var name = ms.GetKey(i);
                var value = ms.Get(i);
                switch (name)
                {
                    case Security.Membership.SessionCookieName:
                        break;
                    default:
                        if (cookis.Exists(r => String.Equals(name, r)) == false)
                        {
                            this.Cookies.Add(new System.Net.Cookie(name, value, "/", this.Domain.Host));
                        }
                        break;
                }
            }
        }
        static Data.ProviderConfiguration _Proxy = new Data.ProviderConfiguration();
        public static Data.ProviderConfiguration Proxy
        {
            get
            {
                return _Proxy;
            }
        }
        SiteProxy siteProxy;
        public int StaticModel
        {
            get;
            private set;
        }
        int CheckStaticPage(string path)
        {
            return CheckStaticPage(this.Site, path);
        }

        public static int CheckStaticPage(SiteConfig config, string path)
        {
            var mv = config.StatusPage.GetEnumerator();
            while (mv.MoveNext())
            {
                var d = mv.Current.Key;
                int splitIndex = d.IndexOf('*');
                bool isOk;
                switch (splitIndex)
                {
                    case -1:
                        isOk = d[0] == '/' ? path.StartsWith(d) : String.Equals(path, d);
                        break;
                    case 0:
                        isOk = path.EndsWith(d.Substring(1));
                        break;
                    default:
                        if (splitIndex == d.Length - 1)
                        {
                            isOk = path.StartsWith(d.Substring(0, d.Length - 1));
                        }
                        else
                        {

                            isOk = path.StartsWith(d.Substring(0, splitIndex)) && path.EndsWith(d.Substring(splitIndex + 1));

                        }

                        break;

                }
                if (isOk)
                {
                    return mv.Current.Value;
                }
            }

            var vk = path;

            var v1 = path.LastIndexOf('.');
            if (v1 > -1)
            {
                vk = vk.Substring(v1).ToLower();

            }
            switch (vk)
            {
                case ".gif":
                case ".ico":
                case ".svg":
                case ".bmp":
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".css":
                case ".less":
                case ".sass":
                case ".scss":
                case ".js":
                case ".webp":
                case ".jsx":
                case ".coffee":
                case ".ts":
                case ".ttf":
                case ".woff":
                case ".woff2":
                case ".wasm":

                    return 0;
                default:
                    return -1;
            }

        }
        public string RawUrl
        {
            get;
            private set;
        }
        public UMC.Net.NetContext Context
        {
            get;
            private set;
        }
        void LoginCheckHtml()
        {

            var sb = new StringWriter();

            sb.WriteLine("<div style=\"margin-left: 60px;\" class=\"umc-proxy-acounts\">");
            sb.WriteLine("<a href=\"/?$=Check\">免密自动绑定</a>");

            sb.WriteLine("<a href=\"/?$=Input\">手输账户绑定</a>");

            sb.WriteLine("</div>");
            sb.WriteLine($"<div style=\"color: #999; line-height: 50px; text-align: center;\">推荐{this.Site.Caption}使用“免密自动绑定”，更简便。");
            sb.WriteLine("</div>");

            this.Context.AddHeader("Cache-Control", "no-store");
            this.Context.ContentType = "text/html; charset=UTF-8";

            using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                  .GetManifestResourceStream("UMC.Proxy.Resources.login-html.html"))
            {
                var str = new System.IO.StreamReader(stream).ReadToEnd();
                this.Context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                {
                    var key = g.Groups["key"].Value.ToLower();
                    switch (key)
                    {

                        case "title":
                            return "请选择账户绑定方式";//, this.Site.Caption);
                        case "html":
                            return sb.ToString();
                    }
                    return "";

                }));

            }


        }
        bool CookieAccountSelectHtml()
        {
            var user = this.Context.Token.Identity(); // UMC.Security.Identity.Current;

            var scookies = DataFactory.Instance().Cookies(this.Site.Root, user.Id.Value).Where(r => String.IsNullOrEmpty(r.Account) == false).OrderBy(r => r.IndexValue).ToList();
            if (scookies.Count > 1)
            {


                var sb = new StringWriter();
                if (scookies.Count == 2)
                {
                    sb.WriteLine("<div style=\"margin-left: 60px;\" class=\"umc-proxy-acounts\">");
                }
                else
                {
                    sb.WriteLine("<div class=\"umc-proxy-acounts\">");
                }
                foreach (var sc in scookies)
                {
                    sb.WriteLine("<a href=\"/?login={0}\">{1}</a>", sc.IndexValue ?? 0, sc.Account);

                }
                sb.WriteLine("</div>");

                this.Context.AddHeader("Cache-Control", "no-store");
                this.Context.ContentType = "text/html; charset=UTF-8";
                using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                      .GetManifestResourceStream("UMC.Proxy.Resources.login-html.html"))
                {
                    var str = new System.IO.StreamReader(stream).ReadToEnd();
                    this.Context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                    {
                        var key = g.Groups["key"].Value.ToLower();
                        switch (key)
                        {

                            case "title":
                                return "您有多个账户，请选择";
                            case "html":
                                return sb.ToString();

                        }
                        return "";

                    }));

                }
                return true;
            }
            return false;

        }
        void LoginHtml(String error, bool isUser)
        {

            using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                     .GetManifestResourceStream("UMC.Proxy.Resources.login.html"))
            {
                this.Context.AddHeader("Cache-Control", "no-store");
                this.Context.ContentType = "text/html; charset=UTF-8";

                var str = new System.IO.StreamReader(stream).ReadToEnd();
                var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                {
                    var key = g.Groups["key"].Value.ToLower();
                    switch (key)
                    {
                        case "user":
                            var Str = "";
                            if (isUser)
                            {
                                using (System.IO.Stream stream2 = typeof(HttpProxy).Assembly
                                                   .GetManifestResourceStream("UMC.Proxy.Resources.user.html"))
                                {
                                    Str = new System.IO.StreamReader(stream2).ReadToEnd().Replace("{name}", this.SiteCookie.Account ?? this.Context.Token.Username);
                                }
                                if (this.SiteCookie.IndexValue > 0)
                                {
                                    return Str;
                                }
                                var upConfig = GetConf(String.Format("SITE_MIME_{0}_UPDATE", Site.Root).ToUpper());
                                if (SiteConfig.CheckMime(upConfig))
                                {

                                    switch (this.Site.Site.UserModel ?? Entities.UserModel.Standard)
                                    {
                                        case Entities.UserModel.Check:
                                            return Str;
                                    }
                                    var updateModel = upConfig["UpdateModel"] as string ?? "Selected";
                                    switch (updateModel)
                                    {
                                        case "Select":
                                        case "Selected":
                                        case "Compel":


                                            using (System.IO.Stream stream2 = typeof(HttpProxy).Assembly
                                                             .GetManifestResourceStream($"UMC.Proxy.Resources.pwd-{updateModel}.html"))
                                            {
                                                Str += new System.IO.StreamReader(stream2).ReadToEnd();
                                            }
                                            break;
                                        default:
                                            break;
                                    }
                                }


                            }
                            return Str;
                        case "action":
                            if (this.RawUrl.EndsWith("/?$=New") || this.RawUrl.EndsWith("/?%24=New"))
                            {
                                return "?$=New";
                            }
                            else if (this.RawUrl.EndsWith("/?$=Input") || this.RawUrl.EndsWith("/?%24=Input"))
                            {
                                return "?$=Input";
                            }
                            else if (this.RawUrl.EndsWith("/?$=Check") || this.RawUrl.EndsWith("/?%24=Check"))
                            {
                                return "?$=Check";
                            }
                            else if (this.RawUrl.StartsWith("/?$=Login") || this.RawUrl.StartsWith("/?%24=Login"))
                            {
                                var callback = this.Context.QueryString["callback"];
                                if (String.IsNullOrEmpty(callback) == false)
                                {
                                    return $"?$=Login&callback={Uri.EscapeDataString(callback)}";
                                }
                                else
                                {
                                    return "?$=Login";
                                }

                            }
                            else
                            {
                                return "?$=Login";
                            }

                        case "title":
                            return isUser ? $"{this.Site.Caption}账户绑定" : $"{this.Site.Caption}登录选择";
                        case "error":
                            return error;
                        case "fields":
                            return UMC.Data.JSON.Serialize(this.FieldHtml(isUser));
                    }
                    return "";

                });
                this.Context.Output.Write(v);

            }
        }

        WebMeta[] FieldHtml(bool isUser)
        {
            var login = GetConf(String.Format("SITE_MIME_{0}_LOGIN", Site.Root).ToUpper());
            var user = this.Context.Token.Identity(); //UMC.Security.Identity.Current;

            var hash = new Hashtable();

            var matchEvaluator = Match(hash, isUser ? "{Username}" : this.SiteCookie.Account, "", "");

            var feilds = login["Feilds"] as Hashtable ?? new Hashtable();
            var list = new List<WebMeta>();
            if (feilds.Count > 0)
            {

                var fd = feilds.GetEnumerator();
                while (fd.MoveNext())
                {
                    var fdKey = fd.Key as string;
                    var mainKey = String.Format("SITE_MIME_{0}_LOGIN_{1}", Site.Root, fdKey).ToUpper();
                    var config = GetConf(mainKey);

                    var script = (config["Script"] as string ?? "").Trim();

                    if (script.StartsWith("[") == false)
                    {
                        continue;
                    }
                    this.Isurlencoded = false;
                    var fConfig = new WebMeta().Put("name", fdKey).Put("title", fd.Value);
                    var changes = new List<String>();
                    var rawUrl = config["RawUrl"] as string;
                    var Method = config["Method"] as string;


                    var getUrl = Regex.Replace(rawUrl, matchEvaluator);
                    var ms = Regex.Matches(getUrl);
                    if (ms.Count > 0)
                    {

                        for (var i = 0; i < ms.Count; i++)
                        {
                            var cKey = ms[i].Groups["key"].Value;
                            if (changes.Exists(c => cKey == c) == false)
                                changes.Add(cKey);
                        }

                    }

                    var value = config["Content"] as string;
                    switch (Method)
                    {
                        case "POST":
                        case "PUT":
                            var valResult = Regex.Replace(value, matchEvaluator);

                            ms = Regex.Matches(valResult);
                            if (ms.Count > 0)
                            {

                                for (var i = 0; i < ms.Count; i++)
                                {
                                    var cKey = ms[i].Groups["key"].Value;
                                    if (changes.Exists(c => cKey == c) == false)
                                        changes.Add(cKey);
                                }

                            }
                            break;
                    }
                    if (changes.Count > 0)
                    {
                        fConfig.Put("change", String.Join(",", changes.ToArray()));
                    }
                    else
                    {
                        fConfig.Put("data", UMC.Data.JSON.Expression(GetConfig(config, matchEvaluator)));
                    }
                    list.Add(fConfig);

                }
            }
            return list.OrderBy(r => r["name"]).ToArray();
        }
        Hashtable GetConf(String mainKey)
        {
            var login = new Hashtable();
            var pconfig = UMC.Data.DataFactory.Instance().Config(mainKey);
            if (pconfig != null)
            {
                var v = UMC.Data.JSON.Deserialize(pconfig.ConfValue) as Hashtable;
                if (v != null)
                {
                    login = v;
                }

            }
            return login;
        }
        void Update(Hashtable feildConfig)
        {

            var newPass = UMC.Data.Utility.Guid(Guid.NewGuid());
            var sb = new System.Text.StringBuilder();
            var htmlConfig = new Hashtable();

            if (this.IsDebug == true)
                this.Loger.WriteLine("更新密码:");
            if (XHR(GetConf(String.Format("SITE_MIME_{0}_UPDATE", Site.Root).ToUpper()), htmlConfig, 0, feildConfig, new StringWriter(sb), "UPDATE", false, newPass))
            {

                var userM = (this.SiteCookie.Model ?? Entities.AccountModel.Standard) | Entities.AccountModel.Changed;
                this.Password = newPass;
                this.SiteCookie.Model = userM;


            }
            else if (sb.Length > 0)
            {
                if (this.IsDebug == true)
                {
                    this.Loger.WriteLine(sb.ToString());
                }
            }
        }
        String GetConfig(String key, String type, MatchEvaluator matchEvaluator, params string[] sParams)
        {
            var mainKey = String.Format("SITE_MIME_{0}_{2}_{1}", Site.Root, key, type).ToUpper();
            return GetConfig(GetConf(mainKey), matchEvaluator, sParams);
        }

        String GetConfig(Hashtable login, MatchEvaluator matchEvaluator, params string[] sParams)
        {


            var script = (login["Script"] as string ?? "");

            script = script.Trim();
            if ((script.StartsWith("{") && script.EndsWith("}")) || (script.StartsWith("[") && script.EndsWith("]")))
            {
                return Regex.Replace(script, matchEvaluator);
            }
            var rawUrl = login["RawUrl"] as string;
            if (String.IsNullOrEmpty(rawUrl))
            {
                return "[]";

            }
            var Header = login["Header"] as string;
            if (String.IsNullOrEmpty(Header) == false)
            {
                this.Isurlencoded = false;
                Header = Regex.Replace(Header, matchEvaluator);
            }

            this.Isurlencoded = true;

            var PathAndQuery = Regex.Replace(rawUrl, matchEvaluator);

            Uri getUrl;

            var sStrDomain = login["Domain"] as string;

            if (String.IsNullOrEmpty(sStrDomain) == false)
            {
                getUrl = new Uri(new Uri(sStrDomain), PathAndQuery);

            }
            else
            {
                getUrl = new Uri(Domain, PathAndQuery);
            }

            var Method = login["Method"] as string;
            if (String.IsNullOrEmpty(Method))
            {
                return "[]";
            }
            var args = new List<String>(sParams);
            var config = new String[0];

            var value = login["Content"] as string;
            switch (Method)
            {
                case "POST":
                case "PUT":
                    var ContentType = login["ContentType"] as string;
                    if (String.IsNullOrEmpty(ContentType))
                    {
                        return "[]";
                    }
                    else
                    {
                        this.Isurlencoded = ContentType.Contains("urlencoded");

                        var valResult = Regex.Replace(value, matchEvaluator);

                        var webr = this.Context.Transfer(getUrl, this.Cookies).Header(Header);

                        webr.ContentType = ContentType;
                        var res = this.Reqesut(webr).Net(Method, valResult);
                        args.Add(res.ReadAsString());

                        if (this.IsDebug == true)
                        {
                            this.Loger.Write(Method);
                            this.Loger.Write(":");
                            this.Loger.WriteLine(getUrl.PathAndQuery);
                            this.Loger.WriteLine(System.Text.UTF8Encoding.UTF8.GetString(webr.Headers.ToByteArray()));

                            this.Loger.WriteLine(valResult);
                            this.Loger.WriteLine();

                            this.Loger.WriteLine("{0} {1} {2}", res.ProtocolVersion, (int)res.StatusCode, res.StatusDescription);

                            this.Loger.WriteLine(Utility.NameValue(res.Headers));
                            this.Loger.WriteLine(args[args.Count - 1]);
                            this.Loger.WriteLine();
                        }
                        int statusCode = Convert.ToInt32(res.StatusCode);
                        if (statusCode >= 500)
                        {
                            var log = new UMC.Data.Entities.Log()
                            {
                                UserAgent = this.Context.UserAgent,
                                Path = String.Format("{0} {1}", Method, getUrl.PathAndQuery),
                                Duration = 0,
                                IP = this.Context.UserHostAddress,
                                Quantity = 1,
                                Key = this.Site.Root,
                                Time = -UMC.Data.Utility.TimeSpan(),
                                Username = User.Name,
                                Status = statusCode,
                                Context = this.SiteCookie.Account
                            };
                            UMC.Data.DataFactory.Instance().Put(log);
                        }
                        if (res.StatusCode != HttpStatusCode.OK)
                        {
                            return "{}";
                        }
                    }
                    break;
                default:
                case "GET":
                    if (String.IsNullOrEmpty(value) == false)
                    {
                        config = SiteConfig.Config(value);
                    }
                    var webr2 = this.Context.Transfer(getUrl, this.Cookies).Header(Header);


                    var res2 = this.Reqesut(webr2).Get();


                    args.Add(res2.ReadAsString());

                    if (res2.StatusCode != HttpStatusCode.OK)
                    {
                        return "{}";
                    }
                    int statusCode2 = Convert.ToInt32(res2.StatusCode);
                    if (statusCode2 >= 500)
                    {
                        var log = new UMC.Data.Entities.Log()
                        {
                            UserAgent = this.Context.UserAgent,
                            Path = String.Format("{0} {1}", Method, getUrl.PathAndQuery),
                            Duration = 0,
                            IP = this.Context.UserHostAddress,
                            Quantity = 1,
                            Key = this.Site.Root,
                            Time = -UMC.Data.Utility.TimeSpan(),
                            Username = User.Name,
                            Status = statusCode2,
                            Context = this.SiteCookie.Account
                        };
                        UMC.Data.DataFactory.Instance().Put(log);
                    }

                    if (this.IsDebug == true)
                    {

                        this.Loger.Write(Method);
                        this.Loger.Write(":");
                        this.Loger.WriteLine(getUrl.PathAndQuery);
                        this.Loger.WriteLine(System.Text.UTF8Encoding.UTF8.GetString(webr2.Headers.ToByteArray()));

                        this.Loger.WriteLine();

                        this.Loger.WriteLine("{0} {1} {2}", res2.ProtocolVersion, (int)res2.StatusCode, res2.StatusDescription);


                        this.Loger.WriteLine(Utility.NameValue(res2.Headers));

                        this.Loger.WriteLine(args[args.Count - 1]);

                        this.Loger.WriteLine();
                    }


                    break;
            }
            if (String.IsNullOrEmpty(script) || String.Equals(script, "none", StringComparison.CurrentCultureIgnoreCase))
            {
                return "{}";
            }

            if (script.StartsWith("function"))
            {
                var JsCode = new List<String>();
                JsCode.Add("function findValue(h, n) {var i = h.indexOf('name=\"' + n + '\"');i = h.indexOf('value=\"', i);var e = h.indexOf('\"', i + 7);return h.substr(i + 7, e - i - 7)}");

                JsCode.Add(script);
                return Regex.Replace(GetScript(JsCode, args, matchEvaluator, config), matchEvaluator);
            }

            else
            {
                var vvsValue = GetKeyValue(args[args.Count - 1], script);
                return UMC.Data.JSON.Serialize(vvsValue);
            }
        }
        Object GetKeyValue(String html, string nvConfig)
        {
            if (nvConfig.StartsWith("[") && nvConfig.Contains("]:"))
            {
                var tfds = new List<String>();

                var keyIndex = nvConfig.IndexOf(':');
                var nv = nvConfig.Substring(0, keyIndex).Trim('[', ']');
                if (String.IsNullOrEmpty(nv) == false)
                {
                    var nvs = nv.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var k in nvs)
                    {
                        var v = k.Trim();
                        if (String.IsNullOrEmpty(v) == false)
                        {
                            tfds.Add(v);
                        }
                    }
                }

                var keyN = nvConfig.Substring(keyIndex + 1).Trim();


                var sc = UMC.Data.JSON.Deserialize(html);
                Array array = null;
                if (html.StartsWith("["))
                {
                    array = sc as Array;
                }
                else if (sc is Hashtable)
                {
                    var scD = sc as Hashtable;
                    if (scD.ContainsKey(keyN))
                    {
                        array = scD[keyN] as Array;
                    }
                    else
                    {
                        int idex = keyN.IndexOf('.');
                        while (idex > 0)
                        {
                            var k = keyN.Substring(0, idex);
                            if (scD.ContainsKey(k))
                            {
                                scD = scD[k] as Hashtable;
                                keyN = keyN.Substring(idex + 1);
                                if (scD.ContainsKey(keyN))
                                {

                                    array = scD[keyN] as Array;
                                    break;
                                }
                                else
                                {
                                    idex = keyN.IndexOf('.');
                                }
                            }
                        }
                    }
                }
                if (array == null)
                {
                    return new int[0];
                }

                var vField = "";
                if (tfds.Count > 0)
                {
                    vField = tfds[tfds.Count - 1];
                    if (tfds.Count > 1)
                    {
                        tfds.RemoveAt(tfds.Count - 1);
                    }
                }
                var list = new List<WebMeta>();

                foreach (var k in array)
                {
                    if (tfds.Count == 0)
                    {
                        list.Add(new WebMeta().Put("Text", k.ToString(), "Value", k.ToString()));
                    }
                    else if (k is Hashtable)
                    {
                        var kd = k as Hashtable;
                        var tkvs = new List<String>();
                        foreach (var tk in tfds)
                        {
                            var v = kd[tk] as string;
                            if (String.IsNullOrEmpty(v) == false)
                            {
                                tkvs.Add(v);
                            }
                        }
                        list.Add(new WebMeta().Put("Text", String.Join("-", tkvs.ToArray())).Put("Value", kd[vField]));

                    }
                }
                return list;

            }
            var nas = SiteConfig.Config(nvConfig);
            var isKey = nas.Contains("KEY-VALUE");
            var vnvs = new Hashtable();

            var formValues = Utility.FromValue(html, isKey);
            var isError = false;
            int valueCount = 0;
            foreach (var name in nas)
            {
                if (String.Equals(name, "KEY-VALUE"))
                {
                    continue;
                }
                else if (String.Equals(name, "KEY-ERROR"))
                {
                    isError = true;

                    continue;

                }
                valueCount++;
                if (formValues.ContainsKey(name))
                {
                    vnvs[name] = formValues[name];
                }
                else
                {
                    int sIndex = html.IndexOf(name);
                    if (sIndex > 1 && sIndex + name.Length < html.Length)
                    {
                        sIndex = sIndex + name.Length;
                        switch (html[sIndex])
                        {
                            case ' ':
                            case '"':
                            case '\'':
                                if (html[sIndex - 1 - name.Length] == html[sIndex])
                                {

                                    while (sIndex < html.Length)
                                    {
                                        sIndex++;
                                        switch (html[sIndex])
                                        {
                                            case '\r':
                                            case '\t':
                                            case '\n':
                                            case ' ':
                                                break;
                                            case ':':
                                            case '=':
                                                var str = GetHtmlValue(html, sIndex + 1);
                                                if (String.IsNullOrEmpty(str) == false)
                                                {
                                                    vnvs[name.Trim(':', '\'', '"', '=').Trim()] = str;
                                                }
                                                sIndex = html.Length;
                                                break;
                                        }
                                    }
                                }
                                break;
                            case ':':
                            case '=':
                                var str2 = GetHtmlValue(html, sIndex + 1);
                                if (String.IsNullOrEmpty(str2) == false)
                                {
                                    vnvs[name.Trim(':', '\'', '"', '=').Trim()] = str2;
                                }
                                break;
                        }
                    }
                }

            }
            if (isError && vnvs.Count != valueCount)
            {
                return "未获取正确的参数";
            }
            return vnvs;
        }
        string GetHtmlValue(string html, int nIndex)
        {

            int start = 0, end = 0;
            char? startStr = null;

            while (nIndex < html.Length)
            {
                switch (html[nIndex])
                {
                    case '\r':
                    case '\t':
                    case '\n':
                    case ' ':

                        if (start > 0 && startStr.HasValue == false)
                        {
                            end = nIndex;
                        }
                        break;
                    case ';':
                    case ',':
                        if (startStr.HasValue == false)
                        {
                            end = nIndex;
                        }
                        break;
                    case '"':
                    case '\'':
                        if (start == 0)
                        {
                            start = nIndex + 1;
                            startStr = html[nIndex];

                        }
                        else if (html[nIndex] == startStr)
                        {
                            end = nIndex;
                        }
                        break;
                    default:
                        if (start == 0)
                        {
                            start = nIndex;
                        }
                        break;
                }
                if (end > 0)
                {
                    break;
                }
                nIndex++;
            }
            if (start > 0 && start < end)
            {
                return html.Substring(start, end - start);
            }
            return null;

        }
        String GetScript(List<String> jscode, List<String> args, MatchEvaluator matchEvaluator, params String[] config)
        {

            foreach (var s in config)
            {
                var surl = s.Trim();
                if (String.IsNullOrEmpty(surl) == false)
                {
                    this.Isurlencoded = true;
                    var url = Regex.Replace(surl, matchEvaluator);
                    if (url.EndsWith(".js"))
                    {
                        if (url.StartsWith("https://") || url.StartsWith("http://") || url.StartsWith("/"))
                        {
                            var t = MD5(url);
                            var staticFile = Data.Reflection.ConfigPath("Static/TEMP/" + t);
                            if (System.IO.File.Exists(staticFile) == false)
                            {
                                if (url.StartsWith("/"))
                                {
                                    jscode.Add(this.Reqesut(this.Context.Transfer(new Uri(this.Domain, url), this.Cookies)).Get().ReadAsString());

                                }
                                else
                                {
                                    jscode.Add(this.Context.Transfer(new Uri(this.Domain, url), this.Cookies).Send("GET", null).ReadAsString());
                                }
                                Data.Utility.Writer(staticFile, jscode[jscode.Count - 1], false);
                            }
                            else
                            {
                                jscode.Add(Data.Utility.Reader(staticFile));
                            }
                        }
                    }
                    else if (url.StartsWith("/"))
                    {
                        var webr = this.Reqesut(this.Context.Transfer(new Uri(this.Domain, url), this.Cookies)).Get();

                        args.Add(webr.ReadAsString());
                    }
                }
            }
            return DataFactory.Instance().Evaluate(String.Join(";", jscode.ToArray()), args.ToArray());
        }

        bool IsDebug;

        String errorMsg;

        String ResetPasswork(Hashtable loginConfig, SiteConfig siteConfig)
        {

            if (String.IsNullOrEmpty(siteConfig.Site.Account))
            {
                if (this.IsDebug == true)
                    this.Loger.WriteLine("未配置检测账户");
                return null;
            }


            var proxy = new HttpProxy(this, siteConfig);


            var checkConfig = GetConf(String.Format("SITE_MIME_{0}_CHECK", proxy.Site.Root).ToUpper());
            if (checkConfig.ContainsKey("Finish"))
            {
                proxy.Cookies = new NetCookieContainer();
                if (String.IsNullOrEmpty(proxy.Site.Home) == false)
                {
                    var r = this.Context.Transfer(new Uri(proxy.Domain, proxy.Site.Home), proxy.Cookies).Get();
                    r.ReadAsString();

                }
                var sb = new System.Text.StringBuilder();
                var htmlConfig = new Hashtable();

                var config = new Hashtable();

                var writer = new StringWriter(sb);
                if (proxy.IsDebug == true)
                    this.Loger.WriteLine("管理员登录:");
                if (checkConfig.ContainsKey("IsNotLoginApi") || proxy.XHR(loginConfig, htmlConfig, 0, config, writer, "LOGIN", false, ""))
                {

                    var newPass = UMC.Data.Utility.Guid(Guid.NewGuid());

                    config["Account"] = this.Context.Token.Username;// UMC.Security.Identity.Current.Name;
                    if (proxy.IsDebug == true)
                        proxy.Loger.WriteLine("检测账户密码:");
                    if (proxy.XHR(checkConfig, htmlConfig, 0, config, writer, "CHECK", false, newPass))
                    {
                        if (config.ContainsKey("ResetPasswork"))
                        {
                            return config["ResetPasswork"] as string;
                        }
                        return newPass;
                    }
                    else
                    {
                        writer.Flush();
                        if (this.IsDebug == true)
                        {
                            this.Loger.WriteLine(sb.ToString());
                        }
                    }
                }
                else
                {
                    errorMsg = "检测账户登录失败导致不能重置密码,请联系应用管理员";
                    writer.WriteLine("管理员账户密码不正确");
                    writer.Flush();
                    if (this.IsDebug == true)
                    {
                        this.Loger.WriteLine(sb.ToString());
                    }
                }
            }
            else
            {
                if (this.IsDebug == true)
                {
                    this.Loger.WriteLine("未配置完善账户检测接口");
                }

            }
            return String.Empty;
        }

        static Regex Regex = new Regex("\\{(?<key>[\\w\\.\\$,\\[\\]-]+)\\}");
        bool Isurlencoded = true;
        bool XHR(Hashtable login, Hashtable htmlConfig, int defaultFeildIndex, Hashtable FeildConfig, System.IO.TextWriter writer, String fieldKey, bool isForm, String newPass)
        {
            NetHttpResponse response = null;
            try
            {
                return XHR(login, htmlConfig, defaultFeildIndex, FeildConfig, writer, fieldKey, isForm, newPass, out response);
            }
            finally
            {
                if (response != null)
                {
                    response.ReadAsString();
                }
            }
        }

        String GetValue(String key, Hashtable FeildConfig, String newPWd)
        {
            switch (key.ToLower())
            {
                case "user":
                    return this.SiteCookie.Account;
                case "pwd":
                    return this.Password;
                case "new":
                    return newPWd;

            }
            return FeildConfig[key] as string;
        }
        MatchEvaluator Match(Hashtable FeildConfig, String newPass)
        {
            return Match(FeildConfig, this.SiteCookie.Account, this.Password, newPass);
        }
        MatchEvaluator Match(Hashtable FeildConfig, String user, String pd, String newPass)
        {
            Func<String, String> func = (key) =>
             {

                 switch (key.ToLower())
                 {

                     case "user":
                     case "username":
                         return Isurlencoded ? Uri.EscapeDataString(user) : user;
                     case "pwd":
                     case "password":
                         return Isurlencoded ? Uri.EscapeDataString(pd ?? "") : (pd ?? "");
                     case "md5pwd":
                         return UMC.Data.Utility.MD5(pd);
                     case "md5new":
                         return UMC.Data.Utility.MD5(newPass);
                     case "newpwd":
                     case "new":
                         return Isurlencoded ? Uri.EscapeDataString(newPass) : newPass;
                     case "time":
                         return UMC.Data.Utility.TimeSpan().ToString();
                     case "mtime":
                         return UMC.Data.Reflection.TimeSpanMilli(DateTime.Now).ToString();
                     default:
                         var nIndex = key.IndexOf('.');
                         if (nIndex > 0)
                         {
                             string fvalue = "";
                             switch (key.Substring(0, nIndex))
                             {
                                 case "hex":
                                     key = key.Substring(4);
                                     fvalue = GetValue(key, FeildConfig, newPass);
                                     if (String.IsNullOrEmpty(fvalue) == false)
                                     {
                                         return UMC.Data.Utility.Hex(System.Text.Encoding.UTF8.GetBytes(fvalue));
                                     }
                                     break;
                                 case "b64":
                                     key = key.Substring(4);
                                     fvalue = GetValue(key, FeildConfig, newPass);
                                     if (String.IsNullOrEmpty(fvalue) == false)
                                     {
                                         if (this.Isurlencoded)
                                         {
                                             return Uri.EscapeDataString(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fvalue)));
                                         }
                                         else
                                         {
                                             return (Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fvalue)));
                                         }
                                     }
                                     break;
                                 case "md5":
                                     key = key.Substring(4);
                                     {
                                         var isb64 = false;
                                         if (key.EndsWith(".b64"))
                                         {
                                             isb64 = true;
                                             key = key.Substring(0, key.Length - 4);
                                         }

                                         fvalue = GetValue(key, FeildConfig, newPass);
                                         if (String.IsNullOrEmpty(fvalue) == false)
                                         {
                                             byte[] mdata;
                                             using (var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider())
                                             {
                                                 mdata = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(fvalue));
                                             }
                                             if (isb64)
                                             {
                                                 if (this.Isurlencoded)
                                                 {
                                                     return Uri.EscapeDataString(Convert.ToBase64String(mdata));
                                                 }
                                                 else
                                                 {
                                                     return Convert.ToBase64String(mdata);
                                                 }
                                             }
                                             else
                                             {
                                                 return UMC.Data.Utility.Hex(mdata);

                                             }
                                         }
                                     }
                                     break;

                                 case "s256":
                                     key = key.Substring(5);
                                     {
                                         var isb64 = false;
                                         if (key.EndsWith(".b64"))
                                         {
                                             isb64 = true;
                                             key = key.Substring(0, key.Length - 4);
                                         }
                                         fvalue = GetValue(key, FeildConfig, newPass);
                                         if (String.IsNullOrEmpty(fvalue) == false)
                                         {

                                             var mdata = SHA256Managed.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(fvalue));
                                             if (isb64)
                                             {
                                                 if (this.Isurlencoded)
                                                 {
                                                     return Uri.EscapeDataString(Convert.ToBase64String(mdata));
                                                 }
                                                 else
                                                 {
                                                     return Convert.ToBase64String(mdata);
                                                 }
                                             }
                                             else
                                             {
                                                 return UMC.Data.Utility.Hex(mdata);

                                             }
                                         }
                                     }
                                     break;
                                 case "sha1":
                                     key = key.Substring(5);
                                     {
                                         var isb64 = false;
                                         if (key.EndsWith(".b64"))
                                         {
                                             isb64 = true;
                                             key = key.Substring(0, key.Length - 4);
                                         }
                                         fvalue = GetValue(key, FeildConfig, newPass);
                                         if (String.IsNullOrEmpty(fvalue) == false)
                                         {
                                             var mdata = SHA1.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(fvalue));
                                             if (isb64)
                                             {
                                                 if (this.Isurlencoded)
                                                 {
                                                     return Uri.EscapeDataString(Convert.ToBase64String(mdata));
                                                 }
                                                 else
                                                 {
                                                     return Convert.ToBase64String(mdata);
                                                 }
                                             }
                                             else
                                             {
                                                 return UMC.Data.Utility.Hex(mdata);
                                             }

                                         }
                                     }
                                     break;
                                 case "hmac":
                                     key = key.Substring(5);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {

                                         var bpwd = FeildConfig[(key.Substring(0, nIndex))] as string;
                                         if (String.IsNullOrEmpty(bpwd) == false)
                                         {
                                             key = key.Substring(nIndex + 1);

                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }


                                             fvalue = GetValue(key, FeildConfig, newPass);
                                             if (String.IsNullOrEmpty(fvalue) == false)
                                             {
                                                 var mdata = new HMACSHA1(Encoding.UTF8.GetBytes(bpwd)).ComputeHash(Encoding.UTF8.GetBytes(fvalue));

                                                 SHA1.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(fvalue));
                                                 if (isb64)
                                                 {
                                                     if (this.Isurlencoded)
                                                     {
                                                         return Uri.EscapeDataString(Convert.ToBase64String(mdata));
                                                     }
                                                     else
                                                     {
                                                         return Convert.ToBase64String(mdata);
                                                     }
                                                 }
                                                 else
                                                 {
                                                     return UMC.Data.Utility.Hex(mdata);
                                                 }
                                             }
                                         }
                                     }
                                     break;
                                 case "aes":
                                     key = key.Substring(4);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {
                                         var nkey = FeildConfig[key.Substring(0, nIndex)] as string;
                                         if (String.IsNullOrEmpty(nkey) == false)
                                         {
                                             key = key.Substring(nIndex + 1);
                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }
                                             var keys = key.Split('.');
                                             switch (keys.Length)
                                             {
                                                 case 1:
                                                     fvalue = GetValue(keys[0], FeildConfig, newPass);
                                                     if (String.IsNullOrEmpty(fvalue) == false)
                                                     {
                                                         if (isb64)
                                                         {
                                                             if (this.Isurlencoded)
                                                             {
                                                                 return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey)));
                                                             }
                                                             else
                                                             {
                                                                 return Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey));
                                                             }
                                                         }
                                                         else
                                                         {
                                                             return UMC.Data.Utility.Hex(UMC.Data.Utility.AES(fvalue, nkey));
                                                         }
                                                     }
                                                     break;
                                                 case 2:
                                                     var iv = FeildConfig[keys[0]] as string;
                                                     fvalue = GetValue(keys[1], FeildConfig, newPass);
                                                     if (String.IsNullOrEmpty(fvalue) == false)
                                                     {
                                                         if (isb64)
                                                         {
                                                             if (this.Isurlencoded)
                                                             {
                                                                 return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey, iv)));
                                                             }
                                                             else
                                                             {
                                                                 return Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey, iv));
                                                             }
                                                         }
                                                         else
                                                         {
                                                             return UMC.Data.Utility.Hex(UMC.Data.Utility.AES(fvalue, nkey, iv));
                                                         }
                                                     }
                                                     break;
                                             }
                                         }
                                     }

                                     break;
                                 case "des":
                                     key = key.Substring(4);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {
                                         var nkey = FeildConfig[key.Substring(0, nIndex)] as string;
                                         if (String.IsNullOrEmpty(nkey) == false)
                                         {
                                             key = key.Substring(nIndex + 1);
                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }
                                             var keys = key.Split('.');
                                             switch (keys.Length)
                                             {
                                                 case 1:
                                                     fvalue = GetValue(keys[0], FeildConfig, newPass);
                                                     if (String.IsNullOrEmpty(fvalue) == false)
                                                     {
                                                         if (isb64)
                                                         {
                                                             if (this.Isurlencoded)
                                                             {
                                                                 return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey)));
                                                             }
                                                             else
                                                             {
                                                                 return Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey));
                                                             }
                                                         }
                                                         else
                                                         {
                                                             return UMC.Data.Utility.Hex(UMC.Data.Utility.DES(fvalue, nkey));
                                                         }
                                                     }
                                                     break;
                                                 case 2:
                                                     var iv = FeildConfig[keys[0]] as string;
                                                     fvalue = GetValue(keys[1], FeildConfig, newPass);
                                                     if (String.IsNullOrEmpty(fvalue) == false)
                                                     {
                                                         if (isb64)
                                                         {
                                                             if (this.Isurlencoded)
                                                             {
                                                                 return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey, iv)));
                                                             }
                                                             else
                                                             {
                                                                 return Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey, iv));
                                                             }
                                                         }
                                                         else
                                                         {
                                                             return UMC.Data.Utility.Hex(UMC.Data.Utility.DES(fvalue, nkey, iv));
                                                         }
                                                     }
                                                     break;
                                             }
                                         }
                                     }

                                     break;
                                 case "pem":
                                     key = key.Substring(4);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {
                                         var pem = FeildConfig[key.Substring(0, nIndex)] as string;
                                         if (String.IsNullOrEmpty(pem) == false)
                                         {
                                             key = key.Substring(nIndex + 1);

                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }
                                             fvalue = GetValue(key, FeildConfig, newPass);
                                             if (String.IsNullOrEmpty(fvalue) == false)
                                             {
                                                 if (isb64)
                                                 {
                                                     if (this.Isurlencoded)
                                                     {
                                                         return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.RSA(pem, fvalue)));
                                                     }
                                                     else
                                                     {
                                                         return Convert.ToBase64String(UMC.Data.Utility.RSA(pem, fvalue));
                                                     }
                                                 }
                                                 else
                                                 {
                                                     return UMC.Data.Utility.Hex(UMC.Data.Utility.RSA(pem, fvalue));
                                                 }
                                             }

                                         }
                                     }
                                     break;
                                 case "rsa":
                                     key = key.Substring(4);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {
                                         var n = FeildConfig[key.Substring(0, nIndex)] as string;
                                         if (String.IsNullOrEmpty(n) == false)
                                         {
                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }
                                             key = key.Substring(nIndex + 1);
                                             nIndex = key.IndexOf('.');

                                             if (nIndex > 0)
                                             {
                                                 var e = FeildConfig[key.Substring(0, nIndex)] as string;
                                                 key = key.Substring(nIndex + 1);

                                                 fvalue = GetValue(key, FeildConfig, newPass);
                                                 if (String.IsNullOrEmpty(fvalue) == false)
                                                 {
                                                     if (isb64)
                                                     {
                                                         if (this.Isurlencoded)
                                                         {
                                                             return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.RSA(n, e, fvalue)));
                                                         }
                                                         else
                                                         {
                                                             return Convert.ToBase64String(UMC.Data.Utility.RSA(n, e, fvalue));
                                                         }
                                                     }
                                                     else
                                                     {
                                                         return UMC.Data.Utility.Hex(UMC.Data.Utility.RSA(n, e, fvalue));
                                                     }
                                                 }
                                             }
                                         }
                                     }

                                     break;
                             }
                         }

                         if (FeildConfig.ContainsKey(key))
                         {
                             return Isurlencoded ? Uri.EscapeDataString(FeildConfig[key] as string ?? "") : FeildConfig[key] as string;
                         }
                         else
                         {
                             var cookie = this.Cookies.GetCookie(key);
                             if (cookie != null)
                             {
                                 return Isurlencoded ? Uri.EscapeDataString(cookie.Value) : cookie.Value;
                             }
                         }
                         return null;
                 }
             };

            MatchEvaluator matchEvaluator = r =>
            {

                var key = r.Groups["key"].Value;
                var kIndex = key.IndexOf('[');
                if (kIndex > 0)
                {
                    var value = func(key.Substring(0, kIndex));
                    if (value == null)
                    {
                        return r.Value;
                    }
                    else
                    {
                        var sValue = key.Substring(kIndex + 1).Trim('[', ']').Trim().Split(',');

                        switch (sValue.Length)
                        {
                            case 1:
                                return value.Substring(UMC.Data.Utility.IntParse(sValue[0], 0));
                            case 2:
                                return value.Substring(UMC.Data.Utility.IntParse(sValue[0], 0), UMC.Data.Utility.IntParse(sValue[1], value.Length));

                            default:
                                return r.Value;
                        }

                    }
                }
                else
                {
                    return func(key) ?? r.Value;
                }

            };
            return matchEvaluator;
        }
        bool XHR(Hashtable login, Hashtable htmlConfig, int defaultFeildIndex, Hashtable FeildConfig, System.IO.TextWriter writer, String fieldKey, bool isForm, String newPass, out NetHttpResponse httpResponse)
        {
            httpResponse = null;
            if (login.ContainsKey("Finish"))
            {

                if (String.IsNullOrEmpty(this.SiteCookie.Account) && String.IsNullOrEmpty(this.Password))
                {
                    return false;
                }
                var username = this.SiteCookie.Account;
                var Password = this.Password;

                var matchEvaluator = Match(FeildConfig, newPass);
                var list = new List<string>();
                list.Add(username);
                list.Add(Password);
                if (string.IsNullOrEmpty(newPass) == false)
                {
                    list.Add(newPass);
                }
                this.Isurlencoded = true;
                var feilds = login["Feilds"] as Hashtable ?? new Hashtable();
                if (feilds.Count > 0)
                {

                    var fd = feilds.Keys.Cast<String>().OrderBy(r => r).GetEnumerator();

                    while (fd.MoveNext())
                    {
                        var fdKey = fd.Current;

                        var fvalue = this.Context.Form.Get(fdKey);
                        if (String.IsNullOrEmpty(fvalue) || isForm == false)
                        {

                            var obj = UMC.Data.JSON.Deserialize(GetConfig(fdKey, fieldKey, matchEvaluator, list.ToArray()));
                            if (obj is Array)
                            {
                                Array array = obj as Array;
                                if (array.Length == 0)
                                {
                                    return false;
                                }
                                else if (array.Length == 1)
                                {
                                    defaultFeildIndex = 0;
                                }
                                if (defaultFeildIndex > -1 && defaultFeildIndex < array.Length)
                                {
                                    var h = array.GetValue(defaultFeildIndex) as Hashtable;
                                    if (h != null)
                                    {
                                        var fValue = h["Value"] as string;
                                        FeildConfig[fdKey] = h["Value"];
                                        if (String.IsNullOrEmpty(fValue))
                                        {

                                            writer.Write("获取到");
                                            writer.Write(feilds[fdKey]);
                                            writer.Write("，格式不正确");
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        writer.Write("获取到");
                                        writer.Write(feilds[fdKey]);
                                        writer.Write("，格式不正确");
                                        return false;

                                    }

                                }
                                else
                                {
                                    htmlConfig[fdKey] = new WebMeta().Put("title", feilds[fdKey]).Put("data", obj);

                                }
                            }
                            else if (obj is Hashtable)
                            {
                                var ms = (obj as Hashtable).GetEnumerator();
                                while (ms.MoveNext())
                                {
                                    FeildConfig[ms.Key] = ms.Value;
                                }
                            }
                            else
                            {
                                writer.Write("获取到");
                                writer.Write(feilds[fdKey]);
                                writer.Write("，格式不正确");
                                return false;
                            }
                        }
                        else
                        {
                            String fText = this.Context.Form.Get(fdKey + "_Text");
                            if (String.IsNullOrEmpty(fText) == false)
                            {

                                FeildConfig[fdKey + "_Text"] = fText;
                            }
                            FeildConfig[fdKey] = fvalue;
                        }
                    }
                    if (htmlConfig.Count > 0)
                    {
                        return false;
                    }

                }
                var rawUrl = login["RawUrl"] as string;
                if (String.IsNullOrEmpty(rawUrl))
                {
                    writer.Write("接口路径未配置");
                    return false;

                }

                var Header = login["Header"] as string;
                if (String.IsNullOrEmpty(Header) == false)
                {
                    this.Isurlencoded = false;
                    Header = Regex.Replace(Header, matchEvaluator);
                }

                this.Isurlencoded = true;


                var PathAndQuery = Regex.Replace(rawUrl, matchEvaluator);

                Uri getUrl = null;

                var sStrDomain = login["Domain"] as string;

                if (String.IsNullOrEmpty(sStrDomain) == false)
                {
                    getUrl = new Uri(new Uri(sStrDomain), PathAndQuery);

                }
                else
                {
                    getUrl = new Uri(Domain, PathAndQuery);
                }


                var Method = login["Method"] as string;
                if (String.IsNullOrEmpty(Method))
                {
                    writer.Write("接口Method未配置");
                    return false;
                }
                var value = login["Content"] as string;

                switch (Method)
                {
                    case "POST":
                    case "PUT":
                        var ContentType = login["ContentType"] as string;
                        if (String.IsNullOrEmpty(ContentType))
                        {
                            writer.Write("接口ContentType未配置");
                            return false;
                        }
                        else
                        {
                            this.Isurlencoded = ContentType.Contains("urlencoded");
                            var valResult = Regex.Replace(value, matchEvaluator);

                            var webR = this.Context.Transfer(getUrl, this.Cookies).Header(Header);
                            webR.ContentType = ContentType;
                            httpResponse = this.Reqesut(webR).Net(Method, valResult);
                            if (this.IsDebug == true)
                            {
                                this.Loger.Write(Method);
                                this.Loger.Write(":");
                                this.Loger.WriteLine(getUrl.PathAndQuery);
                                this.Loger.WriteLine(Utility.NameValue(webR.Headers));
                                this.Loger.WriteLine(valResult);
                                this.Loger.WriteLine();
                            }
                        }
                        break;
                    case "GET":
                        var webr2 = this.Context.Transfer(getUrl, this.Cookies).Header(Header);
                        httpResponse = this.Reqesut(webr2).Get();

                        if (this.IsDebug == true)
                        {
                            this.Loger.Write(Method);
                            this.Loger.Write(":");
                            this.Loger.WriteLine(getUrl.PathAndQuery);
                            this.Loger.WriteLine(System.Text.UTF8Encoding.UTF8.GetString(webr2.Headers.ToByteArray()));

                            this.Loger.WriteLine();
                        }
                        break;
                    default:
                        writer.Write("接口Method不支持");
                        return false;
                }
                if (this.IsDebug == true)
                {
                    this.Loger.WriteLine("{0} {1} {2}", httpResponse.ProtocolVersion, (int)httpResponse.StatusCode, httpResponse.StatusDescription);

                    this.Loger.WriteLine(Utility.NameValue(httpResponse.Headers));
                    this.Loger.WriteLine();
                }
                var finish = login["Finish"] as string;

                if (finish.StartsWith("H:"))
                {
                    var key = finish.Substring(2);
                    var keyIndex = key.IndexOf(':');
                    if (keyIndex > 0)
                    {
                        var v = key.Substring(keyIndex + 1).Trim();
                        key = key.Substring(0, keyIndex);
                        var keyValue = httpResponse.Headers.Get(key);
                        if (String.IsNullOrEmpty(keyValue) == false)
                        {
                            if (String.Equals(keyValue, v))
                            {
                                return true;

                            }
                        }
                    }
                    else if (String.IsNullOrEmpty(httpResponse.Headers.Get(key)) == false)
                    {
                        return true;


                    }
                }
                else if (finish.StartsWith("HE:"))
                {
                    var key = finish.Substring(3);
                    var keyIndex = key.IndexOf(':');
                    if (keyIndex > 0)
                    {
                        var v = key.Substring(keyIndex + 1).Trim();
                        key = key.Substring(0, keyIndex);
                        var keyValue = httpResponse.Headers.Get(key);
                        if (String.IsNullOrEmpty(keyValue) == false)
                        {
                            if (String.Equals(keyValue, v) == false)
                            {
                                return true;
                            }

                        }
                    }
                    else if (String.IsNullOrEmpty(httpResponse.Headers.Get(key)))
                    {
                        return true;
                    }
                }
                else
                {
                    switch (httpResponse.StatusCode)
                    {
                        case HttpStatusCode.Redirect:
                        case HttpStatusCode.RedirectKeepVerb:
                        case HttpStatusCode.RedirectMethod:
                            var Location = httpResponse.Headers.Get("Location");
                            htmlConfig["Location"] = Location;
                            if (String.Equals("Url", finish))
                            {
                                return true;
                            }
                            break;
                        case HttpStatusCode.OK:

                            var body = httpResponse.ReadAsString();
                            _CheckBody = body;

                            if (this.IsDebug == true)
                            {
                                this.Loger.WriteLine(body);
                                this.Loger.WriteLine();
                            }
                            if (finish.StartsWith("E:"))
                            {
                                if (body.Contains(finish.Substring(2)) == false)
                                {
                                    return true;

                                }
                            }
                            else
                            {
                                if (body.Contains(finish) && String.Equals("Url", finish) == false)
                                {
                                    return true;

                                }
                            }
                            break;
                        default:

                            int statusCode = Convert.ToInt32(httpResponse.StatusCode);
                            if (statusCode >= 500)
                            {
                                var log = new UMC.Data.Entities.Log()
                                {
                                    UserAgent = this.Context.UserAgent,
                                    Path = String.Format("{0} {1}", Method, getUrl.PathAndQuery),
                                    Duration = 0,
                                    IP = this.Context.UserHostAddress,
                                    Quantity = 1,
                                    Key = this.Site.Root,
                                    Time = -UMC.Data.Utility.TimeSpan(),
                                    Username = User.Name,
                                    Status = statusCode,
                                    Context = this.SiteCookie.Account
                                };
                                UMC.Data.DataFactory.Instance().Put(log);

                            }
                            return false;

                    }


                }
                return false;
            }
            else
            {
                writer.Write("接口配置未完善");
                return false;
            }

        }
        String _CheckBody;

        bool Login(bool isHome)
        {
            return this.Login(isHome, false);

        }
        bool Login(bool isHome, bool isBody)
        {

            var username = this.Context.Form.Get("Username");


            this.Password = this.Context.Form.Get("Password");

            int fvIndex = -1;
            if (String.IsNullOrEmpty(username) == false)
            {
                this.SiteCookie.Account = username;
                fvIndex = 0;
            }



            var user = this.Context.Token.Identity();

            if (this.Site.Site.UserModel == Entities.UserModel.Quote)
            {
                if (this.SiteCookie.IndexValue == 0)
                {

                    if (String.IsNullOrEmpty(this.Site.Site.Account) == false && this.Site.Site.Account.StartsWith("@"))
                    {
                        var root = this.Site.Site.Account.Substring(1);
                        this.SiteCookie.Account = user.Name;
                        this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(root, this.SiteCookie.user_id.Value, 0));
                        if (String.IsNullOrEmpty(Password))
                        {
                            var home = DataFactory.Instance().WebDomain();
                            var union = Data.WebResource.Instance().Provider["union"] ?? ".";

                            this.Context.Redirect($"{this.Context.Url.Scheme}://{this.Site.Site.Account.Substring(1)}{union}{home}/?$=Login&callback={Uri.EscapeDataString(this.Context.Url.AbsoluteUri)}");
                            return true;
                        }

                    }
                    else
                    {
                        WebServlet.Error(this.Context, "登录异常", String.Format("{0}应用引用模式设置错误，请联系管理员", this.Site.Caption, this.SiteCookie.Account), "");
                        return true;
                    }


                }


            }
            if (String.IsNullOrEmpty(this.SiteCookie.Account) == false && String.IsNullOrEmpty(this.Password))
            {
                this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(Site.Root, this.SiteCookie.user_id.Value, this.SiteCookie.IndexValue ?? 0));
                this.sourceUP = String.Format("{0}{1}", this.SiteCookie.Account, this.Password);

            }
            var login = GetConf(String.Format("SITE_MIME_{0}_LOGIN", Site.Root).ToUpper());
            var autoCheck = false;
            if (String.IsNullOrEmpty(this.SiteCookie.Account) || String.IsNullOrEmpty(this.Password))
            {
                if (this.SiteCookie.IndexValue > 0)
                {

                    LoginHtml("", true);
                    return true;
                }
                switch (this.Site.Site.UserModel ?? Entities.UserModel.Standard)
                {
                    case Entities.UserModel.Standard:
                        LoginHtml("", true);
                        return true;
                    case Entities.UserModel.Checked:

                        this.SiteCookie.Account = user.Name;
                        this.Password = this.ResetPasswork(login, this.Site);
                        if (String.IsNullOrEmpty(this.Password))
                        {
                            WebServlet.Error(this.Context, "登录异常", String.IsNullOrEmpty(errorMsg) ? String.Format("应用中未检测到{1}账户，请联系{0}应用管理员确认账户", this.Site.Caption, this.SiteCookie.Account) : errorMsg, "");
                            return true;
                        }
                        autoCheck = true;
                        this.SiteCookie.Model = (this.SiteCookie.Model ?? Entities.AccountModel.Standard) | Entities.AccountModel.Check | Entities.AccountModel.Changed;
                        this.SiteCookie.ChangedTime = 0;

                        break;

                    case Entities.UserModel.Check:
                        switch (this.Context.QueryString["$"])
                        {
                            case "Input":
                                LoginHtml("", true);
                                return true;
                            case "Check":
                                this.SiteCookie.Account = user.Name;
                                this.Password = this.ResetPasswork(login, this.Site);
                                if (String.IsNullOrEmpty(this.Password))
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.IsNullOrEmpty(errorMsg) ? String.Format("应用中未发现{1}账户，您可联系{0}应用管理员或使用<a href=\"/?$=Input\">其他账户</a>登录", this.Site.Caption, this.SiteCookie.Account) : errorMsg, "");
                                    return true;
                                }
                                autoCheck = true;
                                this.SiteCookie.Model = (this.SiteCookie.Model ?? Entities.AccountModel.Standard) | Entities.AccountModel.Check | Entities.AccountModel.Changed;
                                this.SiteCookie.ChangedTime = 0;
                                break;
                            default:
                                LoginCheckHtml();
                                return true;
                        }
                        break;
                    case Entities.UserModel.Quote:
                        var quoteRoot = this.Site.Site.Account.Substring(1);
                        var home = DataFactory.Instance().WebDomain();
                        var union = Data.WebResource.Instance().Provider["union"] ?? ".";

                        this.Context.Redirect($"{this.Context.Url.Scheme}://{quoteRoot}{union}{home}/?$=Login&callback={Uri.EscapeDataString(this.Context.Url.AbsoluteUri)}");


                        return true;
                    case Entities.UserModel.Share:
                        if (this.ShareUser() == false)
                        {
                            WebServlet.Error(this.Context, "登录异常", String.Format("{0}采用共享账户，但账户却未设置，请联系管理员", this.Site.Caption), "");

                        }
                        break;
                }
            }

            if (login.ContainsKey("IsLoginHTML") && this.Context.HttpMethod == "GET" && isBody == false)
            {
                LoginHtml("", false);
                return true;
            }
            if (login.ContainsKey("IsNotCookieClear") == false)
            {
                this.Cookies = new NetCookieContainer(this.SetCookie);
            }

            var sb = new System.Text.StringBuilder();
            var htmlConfig = new Hashtable();

            var feildConfig = UMC.Data.JSON.Deserialize<Hashtable>(this.SiteCookie.Config) ?? new Hashtable();
            var lkeyIndex = this.RawUrl.IndexOf('?');
            if (lkeyIndex > 0)
            {
                var qs = System.Web.HttpUtility.ParseQueryString(this.RawUrl.Substring(lkeyIndex));
                for (var i = 0; i < qs.Count; i++)
                {
                    var key = qs.GetKey(i);
                    var value = qs.Get(i);
                    if (String.IsNullOrEmpty(key) == false && String.IsNullOrEmpty(value) == false)
                    {
                        switch (key)
                        {
                            case "$":
                                break;
                            default:
                                feildConfig[key] = value;
                                break;
                        }
                    }
                }
            }

            if (this.IsDebug == true)
                this.Loger.WriteLine("用户登录:");
            NetHttpResponse httpResponse = null;
            try
            {
                var isOk = XHR(login, htmlConfig, fvIndex, feildConfig, new StringWriter(sb), "LOGIN", !isBody, "", out httpResponse);

                if (isBody)
                {

                    if (httpResponse.IsReadBody)
                    {
                        httpResponse.Transfer(this.Context);
                    }
                    else
                    {
                        httpResponse.Header(this.Context);
                        this.Context.Output.Write(_CheckBody);
                    }
                    return true;

                }
                if (isOk)
                {
                    return LoginAtfer(htmlConfig, feildConfig, isHome, login, httpResponse);
                }
                else
                {
                    if (sb.Length > 0)
                    {
                        WebServlet.Error(this.Context, "登录配置异常", sb.ToString(), "");
                        if (this.IsDebug == true)
                        {
                            this.Loger.WriteLine(sb.ToString());
                        }
                        return true;
                    }
                    else if (this.SiteCookie.IndexValue != 0)
                    {

                        LoginHtml("账户或密码不正确", true);
                        return true;
                    }
                    else
                    {
                        switch (this.Site.Site.UserModel ?? Entities.UserModel.Standard)
                        {
                            case Entities.UserModel.Standard:

                                LoginHtml("账户或密码不正确", true);
                                return true;
                            case Entities.UserModel.Checked:

                                if (autoCheck)
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.IsNullOrEmpty(errorMsg) ? String.Format("在{0}应用{1}检测账户登录失败，您可联系管理员重置标准账户", this.Site.Caption, user.Name) : errorMsg, "");
                                    return true;
                                }
                                else
                                {
                                    this.Password = this.ResetPasswork(login, this.Site);


                                    if (String.IsNullOrEmpty(this.Password))
                                    {
                                        WebServlet.Error(this.Context, "登录异常", String.IsNullOrEmpty(errorMsg) ? String.Format("在{0}应用{1}检测账户登录失败，您可联系管理员重置标准账户", this.Site.Caption, user.Name) : errorMsg, "");
                                        return true;
                                    }
                                    else
                                    {
                                        this.SiteCookie.Account = user.Name;

                                        this.SiteCookie.Model = (this.SiteCookie.Model ?? Entities.AccountModel.Standard) | Entities.AccountModel.Check | Entities.AccountModel.Changed; ;
                                        this.SiteCookie.ChangedTime = 0;

                                        feildConfig = UMC.Data.JSON.Deserialize<Hashtable>(this.SiteCookie.Config) ?? new Hashtable();
                                        httpResponse.ReadAsString();
                                        if (XHR(login, htmlConfig, 0, feildConfig, new StringWriter(sb), "LOGIN", false, "", out httpResponse))
                                        {
                                            return LoginAtfer(htmlConfig, feildConfig, isHome, login, httpResponse);
                                        }
                                        else
                                        {
                                            WebServlet.Error(this.Context, "登录异常", String.Format("{0}是采用检测密码账户，却不能正常使用，请联系管理员", this.Site.Caption, this.Site.Site.Account.Substring(1)), "");
                                            return true;
                                        }
                                    }
                                }
                            case Entities.UserModel.Check:

                                var am = this.SiteCookie.Model ?? Entities.AccountModel.Standard;
                                if ((am & Entities.AccountModel.Check) == Entities.AccountModel.Check)
                                {
                                    goto case Entities.UserModel.Checked;
                                }
                                else
                                {
                                    DataFactory.Instance().Delete(this.SiteCookie);
                                    LoginCheckHtml();
                                    return true;
                                }

                            case Entities.UserModel.Quote:
                                var currentTime = UMC.Data.Utility.TimeSpan();
                                var q = UMC.Data.Utility.IntParse(this.Context.Token.Data["DeviceQuote"] as string, 0);
                                if (q + 10 > currentTime)
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.Format("{0}是引用账户，多次尝试却没有成功，请联系管理员", this.Site.Caption, this.Site.Site.Account.Substring(1)), "");
                                    return true;
                                }
                                else
                                {
                                    var quoteRoot = this.Site.Site.Account.Substring(1);
                                    this.Context.Token.Put("DeviceQuote", currentTime.ToString()).Commit(Context.UserHostAddress);
                                    var home = DataFactory.Instance().WebDomain();
                                    var union = Data.WebResource.Instance().Provider["union"] ?? ".";

                                    this.Context.Redirect($"{this.Context.Url.Scheme}://{quoteRoot}{union}{home}/?$=Login&callback={Uri.EscapeDataString(this.Context.Url.AbsoluteUri)}");
                                }
                                return true;
                            case Entities.UserModel.Share:

                                if (this.ShareUser() == false)
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.Format("{0}是采用共享账户，却没有设置账户，请联系管理员", this.Site.Caption, this.Site.Site.Account.Substring(1)), "");
                                    return true;
                                }
                                httpResponse.ReadAsString();
                                feildConfig = UMC.Data.JSON.Deserialize<Hashtable>(this.SiteCookie.Config) ?? new Hashtable();
                                if (XHR(login, htmlConfig, 0, feildConfig, new StringWriter(sb), "LOGIN", false, "", out httpResponse))
                                {
                                    return LoginAtfer(htmlConfig, feildConfig, isHome, login, httpResponse);
                                }
                                else
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.Format("{0}是采用共享账户，却不能正常使用，请联系管理员", this.Site.Caption, this.Site.Site.Account.Substring(1)), "");
                                    return true;
                                }
                            default:
                                LoginHtml("账户或密码不正确", true);
                                return true;
                        }



                    }
                }
            }
            finally
            {
                if (httpResponse != null)
                    httpResponse.ReadAsString();
            }
        }
        public bool ShareUser()
        {
            var user = this.Site.Site.Account;
            if (String.IsNullOrEmpty(user) == false)
            {

                var vindex = user.IndexOf("~");
                if (vindex > -1)
                {
                    var nv = user.Substring(0, vindex);
                    var fv = user.Substring(vindex + 1);
                    int start = UMC.Data.Utility.IntParse(nv.Substring(nv.Length - fv.Length), -1);
                    int end = UMC.Data.Utility.IntParse(fv, 0);

                    var index = "0000000" + (start + (Data.Reflection.TimeSpanMilli(DateTime.Now) % (end - start + 1)));
                    this.SiteCookie.Account = nv.Substring(0, nv.Length - fv.Length) + index.Substring(index.Length - fv.Length);
                    this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(this.Site.Root, this.Site.Site.Account));
                    return true;
                }
                else if (user.IndexOf('|') > 0)
                {
                    var us = user.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    this.SiteCookie.Account = us[Data.Reflection.TimeSpanMilli(DateTime.Now) % us.Length];
                    this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(this.Site.Root, this.Site.Site.Account));
                    return true;
                }
                else
                {
                    this.SiteCookie.Account = user;
                    this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(this.Site.Root, this.Site.Site.Account));
                    return true;
                }
            }
            return false;
        }

        bool LoginAtfer(Hashtable htmlConfig, Hashtable fieldConfig, bool isHome, Hashtable loginConfig, NetHttpResponse httpResponse)
        {
            this.IsChangeUser = true;
            var configValue = new Hashtable();
            var fdcem = fieldConfig.GetEnumerator();
            while (fdcem.MoveNext())
            {
                if (fdcem.Key.ToString().StartsWith("_") == false)
                {
                    configValue[fdcem.Key] = fdcem.Value;
                }
            }
            this.SiteCookie.Config = UMC.Data.JSON.Serialize(configValue);

            var IsLoginHTML = loginConfig.ContainsKey("IsLoginHTML");
            if (IsLoginHTML)
            {
                var script = loginConfig["Script"] as string;
                if (String.IsNullOrEmpty(script) == false && String.Equals(script, "none", StringComparison.CurrentCultureIgnoreCase) == false)
                {
                    var html = httpResponse.IsReadBody ? this._CheckBody : httpResponse.ReadAsString();

                    var values = this.GetKeyValue(html, script);
                    var nvs = (values as Hashtable).GetEnumerator();

                    while (nvs.MoveNext())
                    {
                        fieldConfig[nvs.Key] = nvs.Value;
                    }
                }

            }


            switch (this.Site.Site.UserModel ?? Entities.UserModel.Standard)
            {
                case Entities.UserModel.Check:
                case Entities.UserModel.Checked:
                case Entities.UserModel.Standard:
                    var AccountModel = this.SiteCookie.Model ?? Entities.AccountModel.Standard;

                    var changeTime = (this.SiteCookie.ChangedTime ?? 0) + 3600 * 24 * 100;

                    if (String.Equals(this.Context.Form.Get("AutoUpdatePwd"), "YES"))
                    {

                        this.Update(fieldConfig);
                    }
                    else if ((AccountModel & Entities.AccountModel.Changed) == Entities.AccountModel.Changed && changeTime < UMC.Data.Utility.TimeSpan())
                    {
                        this.Update(fieldConfig);

                    }
                    break;

            }
            if (String.Equals("/?$=New", this.RawUrl) || String.Equals("/?%24=New", this.RawUrl))
            {

                this.Context.AddHeader("Cache-Control", "no-store");
                this.Context.ContentType = "text/html; charset=UTF-8";

                using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                               .GetManifestResourceStream("UMC.Proxy.Resources.login-new.html"))
                {
                    stream.CopyTo(this.Context.OutputStream);

                };

                return true;
            }
            if (CheckBrowser() == false)
            {
                return true;

            }
            var callbackKey = loginConfig["Callback"] as string ?? "callback";
            var callback = this.Context.QueryString.Get(callbackKey);
            if (String.IsNullOrEmpty(callback) == false)
            {
                this.Context.Redirect(callback);
                return true;
            }


            if (IsLoginHTML)
            {
                var mainKey = String.Format("SITE_MIME_{0}_LOGIN_HTML", this.Site.Root).ToUpper();
                var config = UMC.Data.DataFactory.Instance().Config(mainKey);
                if (config != null && String.Equals(config.ConfValue, "none") == false)
                {
                    this.Isurlencoded = false;
                    this.Context.AddHeader("Cache-Control", "no-store");
                    this.Context.ContentType = "text/html; charset=UTF-8";

                    using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                   .GetManifestResourceStream("UMC.Proxy.Resources.login-html.html"))
                    {
                        var str = new System.IO.StreamReader(stream).ReadToEnd();
                        var matchEvaluator = Match(fieldConfig, "");
                        this.Context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                        {
                            var key = g.Groups["key"].Value.ToLower();
                            switch (key)
                            {
                                case "title":
                                    return String.Format("{0}账户对接", this.Site.Caption);
                                case "html":
                                    return Regex.Replace(config.ConfValue, matchEvaluator);

                            }
                            return "";

                        }));

                    }
                    return true;
                }
            }

            if (isHome)
            {

                if (htmlConfig.ContainsKey("Location"))
                {
                    var Location = (htmlConfig["Location"] as String);
                    var path = Location;
                    if (Location.StartsWith("http://") || Location.StartsWith("http://"))
                    {
                        path = new Uri(Location).PathAndQuery;
                    }

                    if (IsLoginPath(this.Site, path))
                    {
                        this.Context.Redirect(new Uri(this.Domain, this.Site.Home ?? "/").PathAndQuery);

                    }
                    else
                    {
                        this.Context.Redirect(ReplaceRedirect(Location));
                    }
                }
                else
                {
                    this.Context.Redirect(new Uri(this.Domain, this.Site.Home ?? "/").PathAndQuery);
                }

            }
            return isHome;

        }

        bool SaveCookie()
        {

            if (IsChangeUser == false || this.StaticModel == 0)
            {
                return false;
            }

            var siteCookie = new Entities.Cookie
            {
                Domain = this.Site.Root,
                Time = DateTime.Now,
                user_id = this.SiteCookie.user_id,
                IndexValue = this.SiteCookie.IndexValue

            };
            String strCol = UMC.Data.JSON.Serialize(this.Cookies);
            var isSaveCookie = false;
            if (String.Equals(strCol, this.SiteCookie.Cookies) == false)
            {
                siteCookie.Cookies = strCol;
                this.SiteCookie.Cookies = strCol;
                isSaveCookie = true;
            }

            if (this.IsChangeUser == true)
            {

                var nUP = String.Format("{0}{1}", this.SiteCookie.Account, this.Password);

                if (String.Equals(nUP, this.sourceUP) == false)
                {
                    switch (this.Site.Site.UserModel)
                    {
                        case Entities.UserModel.Quote:

                            if (this.SiteCookie.IndexValue > 0)
                            {
                                UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(Site.Root, siteCookie.user_id.Value, siteCookie.IndexValue ?? 0), this.Password);
                            }
                            break;
                        default:
                            UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(Site.Root, siteCookie.user_id.Value, siteCookie.IndexValue ?? 0), this.Password);

                            break;
                    }
                    siteCookie.Account = this.SiteCookie.Account;
                    siteCookie.ChangedTime = UMC.Data.Utility.TimeSpan();
                    siteCookie.Model = this.SiteCookie.Model;
                    this.sourceUP = nUP;
                }

                siteCookie.Config = this.SiteCookie.Config;
                siteCookie.LoginTime = UMC.Data.Utility.TimeSpan();
                this.IsChangeUser = null;
                isSaveCookie = true;

            }
            if (isSaveCookie)
            {
                DataFactory.Instance().Put(siteCookie);
                return true;
            }
            else if (this.SiteCookie.Time < DateTime.Now.AddSeconds(-300))
            {
                DataFactory.Instance().Put(siteCookie);
                return true;

            }

            return false;
        }
        public NetCookieContainer Cookies
        {
            get;
            private set;
        }

        void Log(int duration)
        {

            var time = (int)((UMC.Data.Reflection.TimeSpanMilli(DateTime.Now) - duration) / 1000);
            String Referrer = null;
            if (this.Context.UrlReferrer != null)
            {
                Referrer = this.Context.UrlReferrer.AbsoluteUri;
            }
            var statusCode = this.Context.StatusCode;
            var log = new UMC.Data.Entities.Log()
            {
                UserAgent = this.Context.UserAgent,
                Referrer = Referrer,
                Path = String.Format("{0} {1}", this.Context.HttpMethod, this.RawUrl),
                Duration = duration,
                IP = this.Context.UserHostAddress,
                Quantity = 1,
                Key = this.Site.Root,
                Time = statusCode >= 400 ? -time : time,
                Username = String.IsNullOrEmpty(User.Name) == false && String.Equals(User.Name, "?") == false ? User.Name : $"G:{UMC.Data.Utility.IntParse(User.Id.Value)}",
                Status = statusCode,
                Context = this.SiteCookie.Account
            };
            UMC.Data.DataFactory.Instance().Put(log);
        }

        void Response(NetHttpResponse httpResponse)
        {
            var headers = httpResponse.Headers;

            for (var i = 0; i < headers.Count; i++)
            {
                var key = headers.GetKey(i);

                switch (key.ToLower())
                {
                    case "set-cookie":
                        var value = headers.Get(i);
                        if (this.Site.Site.AuthType == WebAuthType.All)
                        {
                            this.Context.AddHeader(key, value);
                        }
                        break;
                    case "content-type":
                    case "server":
                    case "connection":
                    case "keep-alive":
                        break;
                    case "content-length":
                    case "transfer-encoding":
                        if (httpResponse.IsHead)
                        {
                            this.Context.AddHeader(key, headers.Get(i));
                        }
                        break;
                    default:
                        this.Context.AddHeader(key, headers.Get(i));
                        break;
                }
            }
            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.Redirect:
                case HttpStatusCode.RedirectKeepVerb:
                case HttpStatusCode.RedirectMethod:
                    this.Context.Redirect(ReplaceRedirect(httpResponse.Headers.Get("Location")));


                    this.Context.OutputFinish();

                    return;
                default:
                    this.Context.StatusCode = Convert.ToInt32(httpResponse.StatusCode);
                    break;
            }
            var ContentType = httpResponse.ContentType;

            if (String.IsNullOrEmpty(ContentType) == false)
            {
                this.Context.ContentType = ContentType;
            }
            else if(httpResponse.StatusCode == HttpStatusCode.OK)
            {
                this.Context.ContentType = "text/plain";
            }
            if (httpResponse.IsHead == false)
            {
                var ContentEncoding = httpResponse.ContentEncoding;

                if (String.IsNullOrEmpty(ContentType) == false && httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    SiteConfig.ReplaceSetting replaceSetting;
                    var vIndex = ContentType.IndexOf(';');
                    if (vIndex > 0)
                    {
                        ContentType = ContentType.Substring(0, vIndex);
                    }


                    if (String.Equals("text/html", ContentType, StringComparison.CurrentCultureIgnoreCase))
                    {
                        httpResponse.ReadAsStream(content =>
                        {
                            content.Position = 0;
                            using (Stream ms = OuterHTML(content, ContentEncoding))
                            {
                                if (ms.Length == 0)
                                {
                                    content.Position = 0;
                                    content.CopyTo(this.Context.OutputStream);
                                }
                                else
                                {
                                    this.Context.ContentLength = ms.Length;
                                    ms.Position = 0;
                                    ms.CopyTo(this.Context.OutputStream);
                                }
                                this.Context.OutputFinish();
                                ms.Close();
                            }
                            content.Close();
                            content.Dispose();

                        }, this.Context.Error);
                        return;
                    }
                    else if (this.CheckPath(this.RawUrl.Split('?')[0], ContentType, out replaceSetting))
                    {
                        httpResponse.ReadAsStream(content =>
                        {

                            content.Position = 0;
                            using (System.IO.Stream ms = this.ReplaceHost(content, ContentEncoding, replaceSetting))
                            {
                                if (ms.Length == 0)
                                {
                                    content.Position = 0;
                                    content.CopyTo(this.Context.OutputStream);
                                }
                                else
                                {
                                    ms.Position = 0;
                                    this.Context.ContentLength = ms.Length;
                                    ms.CopyTo(this.Context.OutputStream);
                                }
                                this.Context.OutputFinish();
                                ms.Close(); ;
                            }
                            content.Close();
                            content.Dispose();

                        }, this.Context.Error);
                        return;

                    }
                }
                if (httpResponse.ContentLength > -1)
                {
                    this.Context.ContentLength = httpResponse.ContentLength;

                }
            }
            httpResponse.ReadAsData((b, i, c) =>
            {
                if (c == 0 && b.Length == 0)
                {
                    if (i == -1)
                    {
                        this.Context.Error(httpResponse.Error);
                    }
                    else
                    {
                        this.Context.OutputFinish();
                    }
                }
                else
                {
                    this.Context.OutputStream.Write(b, i, c);
                }
            });


        }
        public static void Cache(string file, System.IO.Stream ms, String contentType, String contentEncoding)
        {

            using (System.IO.Stream sWriter = UMC.Data.Utility.Writer(file, false))
            {
                var data = System.Text.ASCIIEncoding.UTF8.GetBytes($"ContentType:{contentType}\r\n");
                sWriter.Write(data, 0, data.Length);
                if (String.IsNullOrEmpty(contentEncoding) == false)
                {

                    data = System.Text.ASCIIEncoding.UTF8.GetBytes($"Content-Encoding:{contentEncoding}\r\n\r\n");
                }
                else
                {
                    data = System.Text.ASCIIEncoding.UTF8.GetBytes("\r\n");

                }
                sWriter.Write(data, 0, data.Length);

                ms.CopyTo(sWriter);
                sWriter.Close();
            }
        }
        UMC.Security.Identity User;
        long StartTime;
        void ProcessEnd()
        {
            if ((this.StaticModel != 0 || this.Context.StatusCode >= 400))
            {

                this.Log((int)(UMC.Data.Reflection.TimeSpanMilli(DateTime.Now) - StartTime));

            }
            if (this.Site.Site.AuthType > WebAuthType.All)
            {
                this.SaveCookie();
            }
            if (this.IsDebug && User.IsAuthenticated)
            {
                this.Loger.WriteLine("Cookie:{0}", this.Cookies.GetCookieHeader(this.Domain));

                var file = UMC.Data.Reflection.ConfigPath(String.Format("Static\\log\\{0}\\{1}.log", Site.Root, User.Name));
                if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(file)))
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
                }
                using (FileStream stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {

                    var writer = new System.IO.StreamWriter(stream);
                    writer.Write(this.Loger.ToString());
                    writer.Flush();
                    writer.Close();
                }
            }

            this.Loger.Close();
        }
        public void ProcessRequest()
        {

            this.StartTime = UMC.Data.Reflection.TimeSpanMilli(DateTime.Now);



            if (this.RawUrl.StartsWith("/?$=") || this.RawUrl.StartsWith("/?%24=") || this.RawUrl.StartsWith("/?%24=Login") || this.RawUrl.EndsWith("?$=Login"))
            {
                Login();
                this.ProcessEnd();
                return;
            }
            else if (String.Equals(this.RawUrl, "/?login") || this.RawUrl.StartsWith("/?login="))
            {

                var login = this.Context.QueryString.Get("login");
                if (String.IsNullOrEmpty(login))
                {
                    this.Context.AddHeader("Set-Cookie", String.Format("{0}=0; Expires={1}; HttpOnly; Path=/", DeviceIndex, DateTime.Now.AddYears(-10).ToString("r")));

                    if (CookieAccountSelectHtml())
                    {
                        this.ProcessEnd();
                        return;
                    }
                }
                else if (String.Equals(login, "0") == false)
                {
                    this.Context.AddHeader("Set-Cookie", String.Format("{0}={1}; HttpOnly; Path=/", DeviceIndex, login));

                }
                if (this.Site.Site.UserModel == Entities.UserModel.Bridge)
                {
                    this.Context.Redirect(this.Site.Site.Home ?? "/");
                }
                else
                {
                    this.Context.Redirect("/?$=Login");

                }
                this.ProcessEnd();
                return;
            }
            if (this.StaticModel != 0)
            {
                if (this.Site.Site.UserModel == Entities.UserModel.Bridge)
                {
                    var user = this.Context.Token.Identity();// UMC.Security.Identity.Current;
                    if (user.IsAuthenticated)
                    {
                        this.Context.Headers["umc-request-user-id"] = Uri.EscapeDataString(UMC.Data.Utility.Guid(user.Id.Value));
                        this.Context.Headers["umc-request-user-name"] = Uri.EscapeDataString(this.SiteCookie.Account ?? user.Name);
                        this.Context.Headers["umc-request-user-alias"] = Uri.EscapeDataString(user.Alias);

                    }
                }
                else
                {

                    if (Site.Site.AuthType >= WebAuthType.User)
                    {
                        if (String.IsNullOrEmpty(this.SiteCookie.Account))
                        {
                            this.Login(true);
                            this.ProcessEnd();
                            return;
                        }

                    }
                    if ((this.Context.HttpMethod == "GET" && IsLoginPath(this.Site, this.RawUrl)))
                    {
                        this.Login(true);
                        this.ProcessEnd();
                        return;
                    }

                    if (this.SiteCookie.Time.HasValue)
                    {
                        var authExpire = this.Site.Site.AuthExpire ?? 30;
                        if (authExpire > 0 && this.SiteCookie.Time.Value.AddMinutes(authExpire) < DateTime.Now)
                        {
                            if (String.IsNullOrEmpty(this.SiteCookie.Account) == false)
                            {
                                if (this.Login(false))
                                {
                                    this.ProcessEnd();
                                    return;
                                }
                            }
                        }
                    }

                }

                if (this.siteProxy != null && this.siteProxy.Proxy(this))
                {
                    Context.UseSynchronousIO(this.ProcessEnd);
                    return;
                }
            }
            var getUrl = new Uri(Domain, RawUrl);
            switch (Context.HttpMethod)
            {
                default:
                    {

                        var webReq = this.Reqesut(this.Context.Transfer(getUrl, this.Cookies));

                        SiteConfig.ReplaceSetting replaceSetting;
                        if (this.CheckPath(getUrl.AbsolutePath, this.Context.ContentType, out replaceSetting))
                        {
                            _isInputReplaceHost = (replaceSetting.Model & SiteConfig.HostReplaceModel.Input) == SiteConfig.HostReplaceModel.Input;
                        }

                        if (_isInputReplaceHost && (this.Context.ContentLength ?? 0) > 0)
                        {
                            this.Context.ReadAsStream(content =>
                            {
                                using (var ms = new System.IO.MemoryStream())
                                {
                                    var wr = new System.IO.StreamWriter(ms);
                                    var reader = new System.IO.StreamReader(content);
                                    ReplaceHost(wr, reader, replaceSetting);
                                    wr.Flush();
                                    ms.Position = 0;
                                    webReq.ContentType = this.Context.ContentType;
                                    webReq.Net(this.Context.HttpMethod, ms.ToArray(), this.Response);
                                }
                                content.Close();
                                content.Dispose();

                            }, this.Context.Error);
                            Context.UseSynchronousIO(this.ProcessEnd);
                        }
                        else
                        {
                            webReq.Net(this.Context, this.Response);
                            Context.UseSynchronousIO(this.ProcessEnd);

                        }
                    }
                    break;
                case "GET":
                    {

                        var pmd5Key = getUrl.PathAndQuery;
                        if (this.StaticModel > 0)
                        {
                            switch (this.StaticModel)
                            {
                                case 0:
                                    break;
                                case 1:
                                    break;
                                case 2:
                                    pmd5Key = pmd5Key + this.SiteCookie.Account;
                                    break;
                                default:
                                    pmd5Key = String.Format("{0}_{1}_{2}", pmd5Key, this.SiteCookie.Account, UMC.Data.Utility.TimeSpan() / 60 / this.StaticModel);
                                    break;
                            }
                        }

                        string filename = UMC.Data.Reflection.ConfigPath(String.Format("Cache\\{0}\\{1}", Site.Root, Int64MD5(pmd5Key + this.Site.Caption + this.Site.Site.Version)));

                        var md5 = MD5(filename, "");
                        if (this.StaticModel >= 0)
                        {
                            if (CheckCache(this.Context, filename, md5))
                            {
                                this.ProcessEnd();
                                return;
                            }
                        }

                        if (getUrl.AbsolutePath.EndsWith("/Site.Conf.js"))
                        {
                            this.Context.AddHeader("ETag", md5);
                            var Key = getUrl.AbsolutePath.Substring(0, getUrl.AbsolutePath.LastIndexOf("/")).Trim('/');
                            using (var ms = new MemoryStream())
                            {
                                var writer = new StreamWriter(ms);
                                OuterConfJS(writer, Key);
                                writer.Flush();
                                ms.Position = 0;
                                Cache(filename, ms, "text/javascript", String.Empty);

                                ms.Position = 0;
                                this.Context.ContentLength = ms.Length;
                                ms.CopyTo(this.Context.OutputStream);
                                ms.Close();
                                writer.Close();
                            }
                            this.ProcessEnd();
                            return;
                        }

                        this.Reqesut(Context.Transfer(getUrl, this.Cookies)).Get(httpResponse =>
                        {



                            var headers = httpResponse.Headers;
                            String contentEncoding = null;

                            for (var i = 0; i < headers.Count; i++)
                            {
                                var key = headers.GetKey(i);
                                var keyV = headers.Get(i);

                                switch (key.ToLower())
                                {
                                    case "set-cookie":
                                        if (this.Site.Site.AuthType == WebAuthType.All)
                                        {
                                            this.Context.AddHeader(key, keyV);
                                        }
                                        break;
                                    case "content-type":
                                    case "content-length":
                                    case "server":
                                    case "transfer-encoding":
                                    case "connection":
                                    case "keep-alive":
                                        break;
                                    case "content-encoding":
                                        contentEncoding = keyV;
                                        this.Context.AddHeader("Content-Encoding", keyV);
                                        break;

                                    case "last-modified":
                                    case "etag":

                                        if (this.StaticModel == -1)
                                        {
                                            this.Context.AddHeader(key, keyV);
                                        }
                                        break;
                                    default:
                                        this.Context.AddHeader(key, keyV);
                                        break;
                                }
                            }
                            switch (httpResponse.StatusCode)
                            {
                                case HttpStatusCode.Redirect:
                                case HttpStatusCode.RedirectKeepVerb:
                                case HttpStatusCode.RedirectMethod:

                                    this.Context.Redirect(ReplaceRedirect(httpResponse.Headers.Get("Location")));

                                    this.Context.OutputFinish();
                                    return;
                                default:
                                    this.Context.StatusCode = Convert.ToInt32(httpResponse.StatusCode);
                                    break;
                            }
                            var contentType = httpResponse.ContentType ?? String.Empty;

                            if (String.IsNullOrEmpty(contentType)==false)
                            {
                                this.Context.ContentType = contentType;
                            }
                            else if (httpResponse.StatusCode == HttpStatusCode.OK)
                            {
                                this.Context.ContentType = "text/plain";
                            }

                            var ContentType = contentType;
                            var vIndex = ContentType.IndexOf(';');
                            if (vIndex > 0)
                            {
                                ContentType = ContentType.Substring(0, vIndex);
                            }
                            String ckey = null;
                            SiteConfig.ReplaceSetting replaceSetting = null;
                            int model = 0;
                            if (String.Equals(ContentType, "text/html", StringComparison.CurrentCultureIgnoreCase))
                            {
                                model = 1;

                            }
                            else if (this.CheckPath(getUrl.AbsolutePath, "", out ckey, this.Site.AppendJSConf))
                            {
                                model = 2;
                            }
                            else if (this.CheckPath(getUrl.AbsolutePath, ContentType, out replaceSetting))
                            {
                                model = 3;
                            }
                            else if (String.Equals(ContentType, "text/css", StringComparison.CurrentCultureIgnoreCase) && IsCDN)
                            {
                                model = 4;
                            }
                            else if (this.StaticModel > -1)
                            {

                                model = 5;

                            }

                            if (httpResponse.StatusCode == HttpStatusCode.OK && model > 0)
                            {
                                if (this.StaticModel > -1)
                                {
                                    this.Context.AddHeader("ETag", md5);
                                }

                                httpResponse.ReadAsStream(content =>
                                {
                                    content.Position = 0;

                                    System.IO.Stream ms = content;
                                    switch (model)
                                    {
                                        case 1:
                                            ms = this.OuterHTML(content, contentEncoding);
                                            break;
                                        case 2:
                                            ms = this.OutputAppendJS(content, contentEncoding, MD5(ckey, ""));
                                            break;
                                        case 3:
                                            ms = this.ReplaceHost(content, contentEncoding, replaceSetting);
                                            break;
                                        case 4:
                                            ms = this.OuterCSS(content, contentEncoding);
                                            break;
                                    }
                                    if (ms.Length == 0)
                                    {
                                        content.Position = 0;
                                        content.CopyTo(this.Context.OutputStream);
                                        this.Context.OutputFinish();
                                    }
                                    else
                                    {
                                        ms.Position = 0;
                                        if (this.StaticModel > -1)
                                        {
                                            Cache(filename, ms, contentType, contentEncoding);
                                            ms.Position = 0;
                                        }
                                        this.Context.ContentLength = ms.Length;
                                        ms.CopyTo(this.Context.OutputStream);
                                        this.Context.OutputFinish();
                                    }
                                    ms.Close(); ;

                                    content.Close();
                                    content.Dispose();

                                }, this.Context.Error);

                            }
                            else
                            {
                                if (httpResponse.ContentLength > -1)
                                {
                                    this.Context.ContentLength = httpResponse.ContentLength;

                                }
                                httpResponse.ReadAsData((b, i, c) =>
                                {
                                    if (c == 0 && b.Length == 0)
                                    {
                                        if (i == -1)
                                        {
                                            this.Context.Error(httpResponse.Error);
                                        }
                                        else
                                        {
                                            this.Context.OutputFinish();
                                        }
                                    }
                                    else
                                    {
                                        this.Context.OutputStream.Write(b, i, c);
                                    }
                                });
                            }
                        });
                        this.Context.UseSynchronousIO(this.ProcessEnd);
                    }
                    break;
            }




        }
        void Login()
        {
            var lv = this.Context.QueryString["$"];
            switch (lv)
            {
                case "$":
                    this.Login(true, true);
                    return;
                case "Check":
                case "Input":
                case "Login":
                case "New":
                    this.Login(true);
                    return;
                default:

                    break;
            }
            var login = GetConf(String.Format("SITE_MIME_{0}_LOGIN_{1}", Site.Root, lv).ToUpper());
            if (login != null && login.Count > 0)
            {
                this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(this.Site.Root, this.SiteCookie.user_id.Value, 0));
                var hash = new Hashtable();
                UMC.Data.Utility.AppendDictionary(hash, this.Context.Form);
                var usernmae = hash["Username"] as string;
                hash.Remove("Username");
                this.Context.ContentType = "application/json; charset=utf-8";
                var json = GetConfig(login, this.Match(hash, usernmae ?? this.SiteCookie.Account, this.Password, ""));
                if (String.Equals("[]", json) || String.Equals("{}", json))
                {
                    if (login.Contains("DefautValue"))
                    {
                        var DefautValue = login["DefautValue"] as string;
                        if (String.IsNullOrEmpty(DefautValue) == false)
                        {
                            var sKey = SiteConfig.Config(DefautValue);
                            switch (sKey.Length)
                            {
                                case 0:
                                    break;
                                case 1:
                                    json = UMC.Data.JSON.Serialize(new WebMeta[] { new WebMeta().Put("Text", sKey[0], "Value", sKey[0]) });
                                    break;
                                default:
                                    var ls = new List<WebMeta>();
                                    int l = sKey.Length / 2;
                                    for (var i = 0; i < l; i++)
                                    {
                                        ls.Add(new WebMeta().Put("Text", sKey[0], "Value", sKey[1]));
                                    }
                                    json = UMC.Data.JSON.Serialize(ls);

                                    break;
                            }
                        }
                    }
                }

                this.Context.Output.Write(json);
            }
            else
            {
                var hash = new Hashtable();
                UMC.Data.Utility.AppendDictionary(hash, this.Context.QueryString);

                hash.Remove("$");
                if (String.IsNullOrEmpty(lv) == false)
                {
                    this.Isurlencoded = false;
                    this.Context.ContentType = "application/json; charset=utf-8";
                    this.Context.Output.Write(Regex.Replace(lv, this.Match(hash, "", "", "")));

                }
            }

        }
        bool CheckBrowser()
        {
            var uB = Site.Site.UserBrowser ?? Entities.UserBrowser.All;
            var cilentB = Entities.UserBrowser.All;
            var us = this.Context.UserAgent;
            if (uB == Entities.UserBrowser.All)
            {
                return true;

            }
            else if (String.IsNullOrEmpty(us) == false)
            {
                us = us.ToUpper();
                if (us.Contains("CHROME"))
                {
                    cilentB = Entities.UserBrowser.Chrome;
                }
                else if (us.Contains("FIREFOX"))
                {
                    cilentB = Entities.UserBrowser.Firefox;
                }
                else if (us.Contains("MSIE"))
                {
                    cilentB = Entities.UserBrowser.IE;
                }
                else if (us.Contains("DINGTALK"))
                {
                    cilentB = Entities.UserBrowser.Dingtalk;
                }
                else if (us.Contains("WXWORK") || us.Contains("MICROMESSENGER"))
                {
                    cilentB = Entities.UserBrowser.WeiXin;
                }
                else if (us.Contains("WEBKIT"))
                {
                    cilentB = Entities.UserBrowser.WebKit;
                }
                if ((uB & cilentB) != cilentB)
                {
                    var ts = UMC.Data.Utility.Enum(uB);
                    var htmlKey = uB == Entities.UserBrowser.IE ? "UMC.Proxy.Resources.checkIE.html" : "UMC.Proxy.Resources.check.html";

                    var sb = new List<String>();
                    foreach (var k in ts)
                    {
                        switch (k)
                        {
                            case Entities.UserBrowser.Chrome:
                                sb.Add("谷歌");
                                break;
                            case Entities.UserBrowser.IE:
                                sb.Add("IE");
                                break;
                            case Entities.UserBrowser.Firefox:
                                sb.Add("火狐");
                                break;
                            case Entities.UserBrowser.WebKit:
                                sb.Add("WebKit");
                                break;
                            case Entities.UserBrowser.Dingtalk:
                                sb.Add("钉钉");
                                break;
                            case Entities.UserBrowser.WeiXin:
                                sb.Add("微信");
                                break;
                        }
                    }

                    this.Context.AddHeader("Cache-Control", "no-store");
                    this.Context.ContentType = "text/html; charset=UTF-8";
                    using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                          .GetManifestResourceStream("UMC.Proxy.Resources.check.html"))
                    {

                        var str = new System.IO.StreamReader(stream).ReadToEnd();
                        this.Context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                        {
                            var key = g.Groups["key"].Value.ToLower();
                            switch (key)
                            {
                                case "authurl":
                                    if (uB == Entities.UserBrowser.IE)
                                    {
                                        return new Uri(this.Context.Url, String.Format("/!/{0}/?login", Utility.MD5(this.Context.Token.Id.Value))).AbsoluteUri;//, url.PathAndQuery);//);
                                    }
                                    return "";
                                case "authkey":
                                    return new Uri(this.Context.Url, String.Format("/UMC/{0}/Proxy/Auth", UMC.Data.Utility.Guid(this.Context.Token.Id.Value))).AbsoluteUri;// UMC.Data.Utility.Guid(UMC.Security.AccessToken.Token.Value), authKey);// ; ;//).AbsoluteUri;//, url.PathAndQuery);//);

                                case "isie":
                                    return uB == Entities.UserBrowser.IE ? "yes" : "no";

                                case "desc":
                                    return String.Format("{0}只支持在{1}中使用", Site.Caption, String.Join(",", sb.ToArray()));
                            }
                            return "";

                        }));

                    }




                    return false;

                }
            }


            return true;

        }
        public static bool IsLoginPath(SiteConfig config, String rawUrl)
        {
            foreach (var path in config.LogoutPath)
            {

                if (path.StartsWith("/"))
                {
                    if (String.Equals(rawUrl, path, StringComparison.CurrentCultureIgnoreCase) || rawUrl.StartsWith(path + "?", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }

                }
                if (path.EndsWith("$"))
                {
                    if (String.Equals(rawUrl + "$", path, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public static bool CheckCache(NetContext context, String filename, String md5)
        {
            String match = context.Headers["If-None-Match"];
            if (String.Equals(match, md5))
            {
                context.StatusCode = 304;
                return true;
            }

            if (File.Exists(filename))
            {
                var file = new System.IO.FileInfo(filename);
                var Since = context.Headers["If-Modified-Since"];
                if (String.IsNullOrEmpty(Since) == false)
                {
                    var time = Convert.ToDateTime(Since).ToLocalTime();
                    if (time >= file.LastWriteTimeUtc.ToLocalTime())
                    {
                        context.StatusCode = 304;

                        return true;

                    }
                }
                context.AddHeader("ETag", md5);
                context.AddHeader("Last-Modified", file.LastWriteTimeUtc.ToString("r"));


                using (System.IO.FileStream stream = System.IO.File.OpenRead(filename))
                {
                    var data = new byte[100];
                    var size = stream.Read(data, 0, data.Length);

                    var end = UMC.Data.Utility.FindIndexIgnoreCase(data, 0, size, HttpMimeBody.HeaderEnd);
                    //int ooffset = 0;
                    var ss = System.Text.Encoding.UTF8.GetString(data, 0, end).Split(new String[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);// ';
                    foreach (var heaerValue in ss)
                    {

                        var vi = heaerValue.IndexOf(':');
                        var key = heaerValue.Substring(0, vi);
                        var value = heaerValue.Substring(vi + 1);
                        switch (key)
                        {
                            case "ContentType":
                                context.ContentType = value;// header.ContentType;
                                break;
                            case "Content-Encoding":
                                context.AddHeader("Content-Encoding", value);
                                break;
                        }
                    }
                    context.ContentLength = file.Length - end - 4;
                    size -= end + 4;
                    if (size > 0)
                    {
                        context.OutputStream.Write(data, end + 4, size);
                    }

                    stream.CopyTo(context.OutputStream);
                    stream.Close();
                    stream.Dispose();

                }
                return true;
            }


            return false;
        }
        public HttpWebRequest Reqesut(HttpWebRequest webr)
        {
            if (this.StaticModel != 0)
            {
                WebServlet.WebHeaderConf(webr, this.Site, this.Context);
            }
            if (String.IsNullOrEmpty(this.Host) == false)
            {
                var h = this.Host;
                webr.Headers[HttpRequestHeader.Host] = h;
                if (String.Equals(h, this.Context.Url.Authority) == false)
                {
                    var Referer = webr.Headers[HttpRequestHeader.Referer];
                    if (String.IsNullOrEmpty(Referer) == false)
                    {
                        webr.Headers[HttpRequestHeader.Referer] = String.Format("{0}://{1}{2}", this.Domain.Scheme, h, Referer.Substring(Referer.IndexOf('/', 8)));

                    }
                    var Origin = webr.Headers["Origin"];
                    if (String.IsNullOrEmpty(Origin) == false)
                    {
                        webr.Headers["Origin"] = String.Format("{0}://{1}/", this.Domain.Scheme, h);
                    }
                }

            }
            webr.Timeout = (this.Site.Site.Timeout ?? 100) * 1000;
            return webr;
        }

        bool CheckPath(String path, String ctype, out SiteConfig.ReplaceSetting replaceSetting)
        {
            SiteConfig.HostReplaceModel? key = null;

            replaceSetting = new SiteConfig.ReplaceSetting() { Hosts = new Dictionary<string, Uri>() };

            if (String.IsNullOrEmpty(ctype) == false && this.Site.HostPage.ContainsKey(ctype))
            {
                replaceSetting = this.Site.HostPage[ctype];
                key = replaceSetting.Model;
            }
            var mv = this.Site.HostPage.GetEnumerator();
            while (mv.MoveNext())
            {
                var d = mv.Current.Key;
                int splitIndex = d.IndexOf('*');
                bool isOk;
                switch (splitIndex)
                {
                    case -1:
                        isOk = d[0] == '/' ? path.StartsWith(d) : String.Equals(path, d);
                        break;
                    case 0:
                        isOk = path.EndsWith(d.Substring(1));
                        break;
                    default:
                        if (splitIndex == d.Length - 1)
                        {
                            isOk = path.StartsWith(d.Substring(0, d.Length - 1));
                        }
                        else
                        {
                            isOk = path.StartsWith(d.Substring(0, splitIndex)) && path.EndsWith(d.Substring(splitIndex + 1));
                        }

                        break;

                }
                if (isOk)
                {
                    if (key.HasValue)
                    {
                        key = key.Value | mv.Current.Value.Model;
                    }
                    else
                    {
                        key = mv.Current.Value.Model;
                    }
                    replaceSetting.Model = key.Value;
                    var mm = mv.Current.Value.Hosts.GetEnumerator();
                    while (mm.MoveNext())
                    {
                        replaceSetting.Hosts[mm.Current.Key] = mm.Current.Value;
                    }
                    return true;
                }

            }
            return key.HasValue;
        }
        bool CheckPath(String path, String ctype, out String key, String[] cfs)
        {
            key = "";

            var cKey = "";
            foreach (String d in cfs)
            {
                int splitIndex = d.IndexOf('*');
                bool isOk;
                switch (splitIndex)
                {
                    case -1:
                        isOk = d[0] == '/' ? path.StartsWith(d) : String.Equals(path, d);
                        break;
                    case 0:
                        isOk = path.EndsWith(d.Substring(1));
                        break;
                    default:
                        if (path.Length > splitIndex)
                        {
                            if (splitIndex == d.Length - 1)
                            {
                                isOk = path.StartsWith(d.Substring(0, d.Length - 1));
                            }
                            else
                            {
                                isOk = path.StartsWith(path.Substring(0, splitIndex)) && path.EndsWith(d.Substring(splitIndex + 1));
                            }
                        }
                        else
                        {
                            isOk = false;
                        }
                        break;

                }
                if (isOk)
                {
                    key = d;
                    return true;
                }
                else if (String.Equals(ctype, d))
                {
                    cKey = d;
                }


            }
            if (String.IsNullOrEmpty(cKey) == false)
            {
                key = cKey;
                return true;
            }
            return false;
        }
        public string MD5(String src)
        {
            return MD5(src, this.Site.Site.Caption + this.Site.Site.Version);
        }
        public static string MD5(String src, String cap)
        {
            var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] md = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(src + cap));

            return UMC.Data.Utility.Parse36Encode(UMC.Data.Utility.IntParse(md));
        }
        public static long Int64MD5(String src)
        {
            var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] md = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(src));

            var b = new byte[8];
            for (var i = 0; i < 8; i++)
            {
                b[i] = md[i * 2 + 1];
            }
            return BitConverter.ToInt64(b, 0);
        }


        Stream OutputAppendJS(Stream response, string encoding, String key)
        {

            var mainKey = String.Format("SITE_JS_CONFIG_{0}{1}", this.Site.Root, key).ToUpper();
            var config = UMC.Data.DataFactory.Instance().Config(mainKey);

            if (config == null)
            {
                return response;
            }
            var reader = new System.IO.StreamReader(DataFactory.Instance().Decompress(response, encoding));
            var ms = new System.IO.MemoryStream();
            var writer = new System.IO.StreamWriter(DataFactory.Instance().Compress(ms, encoding));

            int row = -1;
            var isEnd = false;
            if (config != null && String.IsNullOrEmpty(config.ConfValue) == false)
            {
                if (config.ConfValue.Trim().StartsWith(":"))
                {
                    var s = config.ConfValue.IndexOf('\n');
                    var su = config.ConfValue.Substring(0, s).Trim().Trim(':');
                    if (su.EndsWith("$"))
                    {

                        isEnd = true;
                    }
                    row = UMC.Data.Utility.IntParse(su.Trim('$'), -1);
                    config.ConfValue = config.ConfValue.Substring(s);
                }
            }
            var bf = new Char[1];
            var is_tr = false;
            int index = 1;
            if (row == 0)
            {
                is_tr = true;
                writer.WriteLine(config.ConfValue.Replace("{webr}", this.WebResource + "/" + this.MD5("") + "/")); ;
                if (isEnd)
                {
                    writer.Flush();
                    return ms;
                }
            }

            while (reader.ReadBlock(bf, 0, 1) > 0)
            {
                writer.Write(bf[0]);
                if (bf[0] == '\n')
                {
                    if (index == row)
                    {
                        is_tr = true;
                        writer.WriteLine(config.ConfValue.Replace("{webr}", this.WebResource + "/" + this.MD5("") + "/")); ;
                        if (isEnd)
                        {
                            break;
                        }
                    }
                    index++;
                }
            }
            if (is_tr == false)
            {
                writer.WriteLine(config.ConfValue.Replace("{webr}", this.WebResource + "/" + this.MD5("") + "/")); ;
            }

            writer.Flush();
            return ms;
        }
        bool _isInputReplaceHost;
        String ReplaceRawUrl(String rawUrl)
        {

            var sb = new System.Text.StringBuilder();
            char last = char.MinValue;

            var l = rawUrl.Length;
            for (var i = 0; i < l; i++)
            {
                var c = rawUrl[i];
                switch (c)
                {
                    case '?':

                        sb.Append(c);
                        if (i + 1 < l)
                        {
                            SiteConfig.ReplaceSetting replaceSetting;
                            if (this.CheckPath(sb.ToString(0, sb.Length - 1), String.Empty, out replaceSetting))
                            {
                                if ((replaceSetting.Model & SiteConfig.HostReplaceModel.Input) == SiteConfig.HostReplaceModel.Input)
                                {
                                    _isInputReplaceHost = true;
                                    this.ReplaceHost(new StringWriter(sb), new StringReader(rawUrl.Substring(i + 1)), replaceSetting);
                                }
                                else
                                {
                                    sb.Append(rawUrl.Substring(i + 1));

                                }
                            }
                            else
                            {

                                sb.Append(rawUrl.Substring(i + 1));
                            }

                        }
                        return sb.ToString();


                    case '/':

                        if (last != c)
                        {
                            sb.Append(c);
                            last = c;
                        }
                        break;
                    default:

                        sb.Append(c);
                        last = c;
                        break;
                }
            }
            return sb.ToString();

        }

        void ReplaceHost(System.IO.TextWriter writer, TextReader reader, SiteConfig.ReplaceSetting rpsetting)
        {

            var buffer = new List<Char>();
            var host = this.Context.Url.Host;
            var scheme = String.Format("{0}://", this.Context.Url.Scheme);
            var dScheme = Uri.EscapeDataString(scheme);

            var host2 = this.Domain.Authority;
            if (String.IsNullOrEmpty(Site.Site.Host) == false && String.Equals(Site.Site.Host, "*") == false)
            {
                if (host2.IndexOf(':') > -1)
                {
                    host2 = String.Format("{0}:{1}", Site.Site.Host, this.Domain.Port);
                }
                else
                {
                    host2 = Site.Site.Host;
                }
            }

            var isRmPort = false;
            var isPort = false;

            var hash = new Dictionary<string, String[]>();
            hash[host] = new String[] { scheme, dScheme, host2, Uri.EscapeDataString(host2), $"{this.Domain.Scheme}://", Uri.EscapeDataString($"{this.Domain.Scheme}://") }; ;


            var bsize = host.Length;
            var minSize = bsize;
            if (rpsetting.Hosts.Count > 0)
            {
                var hem = rpsetting.Hosts.GetEnumerator();
                while (hem.MoveNext())
                {
                    var value = hem.Current.Value;
                    hash[hem.Current.Key] =
                        new String[] { scheme, dScheme, value.Authority, Uri.EscapeDataString(value.Authority), $"{value.Scheme}://", Uri.EscapeDataString($"{value.Scheme}://"), };
                    var hl = hem.Current.Key.Length;
                    if (bsize < hl)
                    {
                        bsize = hl;
                    }
                    if (minSize > hl)
                    {
                        minSize = hl;
                    }
                }
            }
            bsize += +dScheme.Length;


            var bf = new char[1];
            while (reader.ReadBlock(bf, 0, 1) > 0)
            {
                var c = bf[0];
                if (isRmPort)
                {
                    if (c > 47 && c < 58)
                    {

                    }
                    else
                    {
                        isRmPort = false;

                        buffer.Add(c);
                    }
                }
                else
                {

                    buffer.Add(c);
                }
                if (isPort)
                {
                    switch (buffer.Count)
                    {
                        case 1:
                            if (c == ':')
                            {
                                isRmPort = true;
                                isPort = false;
                                buffer.Clear();
                            }
                            else
                            {
                                isPort = c == '%';
                            }

                            break;
                        case 2:
                            isPort = c == '3';
                            break;
                        case 3:
                            if (c == 'A')
                            {
                                isRmPort = true;

                                buffer.Clear();
                            }
                            isPort = false;
                            break;
                    }
                }
                else if (minSize <= buffer.Count)
                {
                    var isFind = false;

                    var em = hash.GetEnumerator();
                    while (em.MoveNext())
                    {
                        var h = em.Current.Key;

                        if (EndsWith(buffer, h))
                        {
                            var vs = em.Current.Value;
                            isFind = true;
                            buffer.RemoveRange(buffer.Count - h.Length, h.Length);
                            if (EndsWith(buffer, vs[0]))
                            {
                                buffer.RemoveRange(buffer.Count - vs[0].Length, vs[0].Length);

                                writer.Write(buffer.ToArray());
                                writer.Write(vs[4]);
                                writer.Write(vs[2]);

                            }
                            else if (EndsWith(buffer, vs[1]))
                            {
                                buffer.RemoveRange(buffer.Count - vs[1].Length, vs[1].Length);

                                writer.Write(buffer.ToArray());
                                writer.Write(vs[5]);
                                writer.Write(vs[3]);
                            }
                            else
                            {
                                writer.Write(buffer.ToArray());
                                writer.Write(vs[2]);
                            }
                            buffer.Clear();
                            isPort = true;
                            break;
                        }
                    }

                    if (isFind == false)
                    {
                        if (buffer.Count >= bsize)
                        {

                            writer.Write(buffer[0]);

                            buffer.RemoveAt(0);
                        }
                    }
                }
                else if (buffer.Count >= bsize)
                {

                    writer.Write(buffer[0]);

                    buffer.RemoveAt(0);
                }


            }
            writer.Write(buffer.ToArray());


        }
        void ReplaceHost(System.IO.TextWriter writer, TextReader reader, Dictionary<String, String[]> hosts)
        {


            var hLength = 0;
            var em = hosts.GetEnumerator();
            while (em.MoveNext())
            {
                var Ht = em.Current.Key;
                if (hLength < Ht.Length)
                {
                    hLength = Ht.Length;
                }
            }

            var strHttps = "https://";
            var strHttp = "http://";
            var httpsEncode = Uri.EscapeDataString(strHttps);
            var httpEncode = Uri.EscapeDataString(strHttp);

            var bsize = 14 + hLength;

            var bf = new Char[1];
            var isFind = false;
            var isPort = false;
            var isEncodePort = false;
            var buffer = new List<Char>();
            while (reader.ReadBlock(bf, 0, 1) > 0)
            {
                switch (bf[0])
                {
                    case ' ':
                    case '\r':
                    case '\t':
                    case '\n':
                        isPort = false;
                        writer.Write(buffer.ToArray(), 0, buffer.Count);
                        writer.Write(bf[0]);
                        buffer.Clear();
                        break;
                    case ':':
                        if (isFind)
                        {
                            isPort = true;
                        }
                        else
                        {
                            if (buffer.Count == bsize)
                            {
                                writer.Write(buffer[0]);
                                buffer.RemoveAt(0);
                            }

                            buffer.Add(bf[0]);

                        }
                        break;
                    default:
                        if (isPort)
                        {
                            if (bf[0] > 47 && bf[0] < 58)
                            {
                                break;
                            }
                            else
                            {
                                isPort = false;
                            }
                        }
                        else if (isFind && bf[0] == '%')
                        {

                            isEncodePort = true;
                        }
                        if (buffer.Count == bsize)
                        {
                            writer.Write(buffer[0]);
                            buffer.RemoveAt(0);
                        }

                        buffer.Add(bf[0]);
                        if (isEncodePort && buffer.Count == 3)
                        {
                            isEncodePort = false;
                            var s = new String(buffer.ToArray());
                            if (String.Equals("%3A", s, StringComparison.CurrentCultureIgnoreCase))
                            {
                                isPort = true;

                                buffer.Clear();
                            }
                        }
                        break;

                }
                isFind = false;
                var hem = hosts.GetEnumerator();
                while (hem.MoveNext())
                {
                    var khost = hem.Current.Key;
                    var khostVs = hem.Current.Value;
                    if (EndsWith(buffer, khost))
                    {
                        isFind = true;
                        buffer.RemoveRange(buffer.Count - khost.Length, khost.Length);
                        var isEncode = false;
                        if (EndsWith(buffer, strHttps))
                        {

                            buffer.RemoveRange(buffer.Count - strHttps.Length, strHttps.Length);

                            writer.Write(buffer.ToArray());
                            writer.Write(khostVs[2]);
                        }
                        else if (EndsWith(buffer, strHttp))
                        {

                            buffer.RemoveRange(buffer.Count - strHttp.Length, strHttp.Length);

                            writer.Write(buffer.ToArray());
                            writer.Write(khostVs[2]);
                        }
                        else if (EndsWith(buffer, httpsEncode))
                        {
                            isEncode = true;
                            buffer.RemoveRange(buffer.Count - httpsEncode.Length, httpsEncode.Length);

                            writer.Write(buffer.ToArray());
                            writer.Write(Uri.EscapeDataString(khostVs[2]));
                        }
                        else if (EndsWith(buffer, httpEncode))
                        {
                            isEncode = true;
                            buffer.RemoveRange(buffer.Count - httpEncode.Length, httpEncode.Length);

                            writer.Write(buffer.ToArray());
                            writer.Write(Uri.EscapeDataString(khostVs[2]));
                        }
                        else
                        {
                            writer.Write(buffer.ToArray());
                        }
                        if (isEncode)
                        {
                            writer.Write(Uri.EscapeDataString(khostVs[1]));
                        }
                        else
                        {
                            writer.Write(khostVs[1]);
                        }
                        buffer.Clear();
                        break;
                    }
                }
            }
            if (buffer.Count > 0)
            {
                writer.Write(buffer.ToArray());
            }
            writer.Flush();
            //return ms;
        }

        String ReplaceRedirect(String redirect)
        {

            var host = this.Domain.Host;
            var host2 = this.Context.Url.Authority;
            var nowScheme = String.Format("{0}://", this.Context.Url.Scheme);

            var nowScheme2 = nowScheme;
            var oldScheme = String.Format("{0}://", this.Domain.Scheme);


            var hosts = new Dictionary<String, String[]>();

            hosts[host] = new string[] { oldScheme, host2, nowScheme };
            var host3 = this.Context.Url.Host;
            hosts[host3] = new string[] { oldScheme, host2, nowScheme };



            if (String.IsNullOrEmpty(this.Host) == false)
            {
                var hStr = Site.Site.Host;
                hosts[hStr] = new string[] { oldScheme, host2, nowScheme };
            }

            SiteConfig.ReplaceSetting rpsetting;
            this.CheckPath("", "Redirect", out rpsetting);
            if (rpsetting != null && rpsetting.Hosts.Count > 0)
            {
                var em = rpsetting.Hosts.GetEnumerator();
                while (em.MoveNext())
                {
                    var Ht = em.Current.Value.Host;
                    hosts[Ht] = new string[] { em.Current.Value.Scheme + "://", em.Current.Key, nowScheme2 };
                }

            }

            var writer = new StringWriter();
            var reader = new StringReader(redirect);
            ReplaceHost(writer, reader, hosts);
            var url = writer.ToString();
            var l = url.IndexOf(host2);
            if (l == 7 || l == 8)
            {
                return url.Substring(url.IndexOf('/', 9));
            }
            return url;
        }


        Stream ReplaceHost(Stream response, string encoding, SiteConfig.ReplaceSetting rpsetting)
        {

            var host = this.Domain.Host;
            var host2 = this.Context.Url.Authority;
            var nowScheme = String.Format("{0}://", this.Context.Url.Scheme);

            var nowScheme2 = nowScheme;
            var oldScheme = String.Format("{0}://", this.Domain.Scheme);

            var rp = rpsetting.Model;
            if ((rp & SiteConfig.HostReplaceModel.Remove) == SiteConfig.HostReplaceModel.Remove)
            {
                host2 = "";
                nowScheme = "";
            }
            else if ((rp & SiteConfig.HostReplaceModel.Replace) != SiteConfig.HostReplaceModel.Replace)
            {
                return response;
            }
            var hosts = new Dictionary<String, String[]>();

            hosts[host] = new string[] { oldScheme, host2, nowScheme };
            var host3 = this.Context.Url.Host;
            hosts[host3] = new string[] { oldScheme, host2, nowScheme };



            if (String.IsNullOrEmpty(this.Host) == false && String.Equals(Site.Site.Host, "*") == false)
            {
                var hStr = Site.Site.Host;
                if (String.IsNullOrEmpty(hStr) == false)
                {
                    hosts[hStr] = new string[] { oldScheme, host2, nowScheme };
                }
            }
            if (rpsetting.Hosts.Count > 0)
            {
                var em = rpsetting.Hosts.GetEnumerator();
                while (em.MoveNext())
                {
                    var Ht = em.Current.Value.Host;
                    hosts[Ht] = new string[] { em.Current.Value.Scheme + "://", em.Current.Key, nowScheme2 };
                }

            }


            var reader = new System.IO.StreamReader(DataFactory.Instance().Decompress(response, encoding));
            var ms = new System.IO.MemoryStream();
            var writer = new System.IO.StreamWriter(DataFactory.Instance().Compress(ms, encoding));
            ReplaceHost(writer, reader, hosts);
            return ms;
        }

        void OuterConfJS(System.IO.TextWriter writer, String Key)
        {
            var mainKey = String.Format("SITE_JS_CONFIG_{0}{1}", this.Site.Root, Key).ToUpper();
            var config = UMC.Data.DataFactory.Instance().Config(mainKey);
            this.Context.ContentType = "text/javascript";
            writer.WriteLine();
            if (config != null)
            {
                writer.WriteLine(config.ConfValue.Replace("{webr}", this.WebResource + "/" + this.MD5("") + "/"));

            }
        }

        Stream OuterHTML(Stream response, string encoding)
        {

            var pathKey = this.RawUrl.Split('?')[0];


            var jsKey = "";
            var isAppendJS = this.CheckPath(pathKey, "", out jsKey, this.Site.AppendJSConf);
            if (isAppendJS)
            {
                jsKey = MD5(jsKey, "");
            }

            var host = this.Domain.Host;
            var host2 = this.Context.Url.Host;
            var port2 = String.Format(":{0}", this.Context.Url.Port);
            if (String.Equals(host2, this.Context.Url.Authority))
            {
                port2 = "";
            }
            var nowScheme = String.Format("{0}://", this.Context.Url.Scheme);
            var oldScheme = String.Format("{0}://", this.Domain.Scheme);

            SiteConfig.HostReplaceModel hostRpMode = SiteConfig.HostReplaceModel.Input;// this.Site.HostPage[pathKey];
            SiteConfig.ReplaceSetting replaceSetting;
            if (this.CheckPath(pathKey, "text/html", out replaceSetting))
            {
                hostRpMode = replaceSetting.Model;

            }
            var httpScheme = "http://";
            var httpsScheme = "https://";
            var encodeHttpsScheme = Uri.EscapeDataString(httpsScheme);
            var encodeHttpScheme = Uri.EscapeDataString(httpScheme);

            if (isAppendJS == false && hostRpMode == SiteConfig.HostReplaceModel.Input)
            {
                return response;
            }
            var isCDN = (hostRpMode & SiteConfig.HostReplaceModel.CDN) == SiteConfig.HostReplaceModel.CDN;
            var isScript = (hostRpMode & SiteConfig.HostReplaceModel.Script) == SiteConfig.HostReplaceModel.Script;




            var sv1 = MD5("V");

            var hosts = new Dictionary<String, String[]>();
            var hLength = host.Length;
            hosts[host] = new string[] { oldScheme, host2, port2, nowScheme };
            hosts[host2] = new string[] { oldScheme, host2, port2, nowScheme };

            if (hLength < host2.Length)
            {
                hLength = host2.Length;
            }

            if (String.IsNullOrEmpty(this.Host) == false)
            {
                var hStr = Site.Site.Host;
                hosts[hStr] = new string[] { oldScheme, host2, port2, nowScheme };
                if (hLength < hStr.Length)
                {
                    hLength = hStr.Length;
                }


            }
            if (replaceSetting != null)
            {
                if (replaceSetting.Hosts.Count > 0)
                {
                    var em = replaceSetting.Hosts.GetEnumerator();
                    while (em.MoveNext())
                    {
                        var Ht = em.Current.Value.Host;
                        if (hLength < Ht.Length)
                        {
                            hLength = Ht.Length;
                        }
                        var port3 = String.Format(":{0}", em.Current.Value.Port);
                        if (String.Equals(host2, this.Context.Url.Authority))
                        {
                            port3 = "";
                        }
                        hosts[Ht] = new string[] { em.Current.Value.Scheme + "://", em.Current.Key, port3, nowScheme };
                    }

                }
            }

            var bsize = 14 + hLength;



            var reader = new System.IO.StreamReader(DataFactory.Instance().Decompress(response, encoding));
            var ms = new System.IO.MemoryStream();
            var writer = new System.IO.StreamWriter(DataFactory.Instance().Compress(ms, encoding));


            var buffer = new List<char>();
            var bf = new char[1];
            bool isTag = false; ;
            var tag = "";
            var isEncodePort = false;
            var isFind = false;
            var isPort = false;
            var isUrl = false;
            var isUrlStart = false;
            var isdomainUrl = false;
            char startP = '-';
            while (reader.ReadBlock(bf, 0, 1) > 0)
            {
                switch (bf[0])
                {

                    case '\r':
                    case '\t':
                    case '\n':
                        isPort = false;
                        writer.Write(buffer.ToArray());//, 0, buffer.Count);
                        writer.Write(bf[0]);
                        buffer.Clear();
                        break;
                    case ' ':
                        isPort = false;
                        if (isTag)
                        {
                            isTag = false;
                            tag = new string(buffer.ToArray());
                        }
                        writer.Write(buffer.ToArray());
                        writer.Write(bf[0]);
                        buffer.Clear();
                        break;
                    case '<':
                        isPort = false;
                        isTag = true;
                        tag = String.Empty;
                        writer.Write(buffer.ToArray());
                        buffer.Clear();
                        buffer.Add(bf[0]);

                        break;
                    case '>':
                        isPort = false;
                        if (isTag)
                        {
                            isTag = false;
                            if (isAppendJS)
                            {
                                if (String.IsNullOrEmpty(tag))
                                {
                                    if (EndsWith(buffer, "</head"))
                                    {
                                        var src = String.Format(isCDN || isScript ? "{0}/{1}/{2}/Site.Conf.js" : "/{2}/Site.Conf.js", this.WebResource, this.MD5(""), jsKey);
                                        writer.WriteLine("<script src=\"{0}\"></script>", src);
                                        writer.Write("</head>");

                                        buffer.Clear();
                                        break;
                                    }
                                }
                            }
                        }
                        tag = String.Empty;
                        writer.Write(buffer.ToArray());
                        writer.Write(bf[0]);
                        buffer.Clear();
                        break;
                    case '=':
                        isPort = false;

                        if (isCDN || isScript)
                        {
                            if (isUrlStart == false)
                            {
                                if (String.IsNullOrEmpty(tag) == false)
                                {

                                    switch (tag.ToLower())
                                    {
                                        case "<link":

                                            if (EndsWith(buffer, "href"))
                                            {
                                                isUrl = true;
                                            }
                                            break;
                                        case "<script":
                                        case "<img":
                                            if (EndsWith(buffer, "src"))
                                            {
                                                isUrl = true;
                                            }
                                            break;
                                    }
                                }

                                writer.Write(buffer.ToArray());
                                writer.Write(bf[0]);
                                buffer.Clear();
                            }
                            else
                            {
                                buffer.Add(bf[0]);
                            }
                        }
                        else
                        {
                            goto default;
                        }
                        break;
                    case '\'':
                    case '"':
                        if (isUrl)
                        {
                            isUrlStart = true;
                            isUrl = false;
                            startP = bf[0];

                        }
                        else if (isUrlStart)
                        {
                            if (startP == bf[0])
                            {
                                isUrlStart = false;
                                var src = new String(buffer.ToArray());
                                if (src.StartsWith(httpScheme) || src.StartsWith(httpsScheme) || src.StartsWith("//"))
                                {
                                    var url = new Uri(src);
                                    if (String.Equals(url.Host, host))
                                    {
                                        if (isScript)
                                        {

                                            src = String.Format("{0}/{1}{2}", this.WebResource, this.MD5(url.PathAndQuery), url.PathAndQuery);

                                        }
                                        else
                                        {
                                            if (this.CheckStaticPage(url.AbsolutePath) == 0)
                                            {

                                                src = String.Format("{0}/{1}{2}", this.WebResource, this.MD5(url.PathAndQuery), url.PathAndQuery);
                                            }
                                        }

                                    }
                                    else
                                    {
                                        var hem = hosts.GetEnumerator();
                                        while (hem.MoveNext())
                                        {
                                            if (String.Equals(url.Host, hem.Current.Key))
                                            {
                                                var hv = hem.Current.Value;
                                                src = String.Format("{0}{1}{2}{3}", hv[3], hv[1], hv[2], url.PathAndQuery);
                                                break;
                                            }
                                        }

                                        if (src.IndexOf("?") > -1)
                                        {
                                            src = String.Format("{0}&_v={1}", src, sv1);
                                        }
                                        else
                                        {
                                            src = String.Format("{0}?_v={1}", src, sv1);
                                        }
                                    }
                                }
                                else
                                {
                                    if (src.StartsWith("/UMC.Conf") == false)
                                    {

                                        switch (src[0])
                                        {
                                            case '{':
                                                break;
                                            default:

                                                if (isScript)
                                                {
                                                    src = new Uri(new Uri(this.Domain, this.RawUrl), src).PathAndQuery;
                                                    src = String.Format("{0}/{1}{2}", this.WebResource, this.MD5(src), src);

                                                }
                                                else
                                                {
                                                    if (this.CheckStaticPage(src.Split('?')[0]) == 0)
                                                    {
                                                        src = new Uri(new Uri(this.Domain, this.RawUrl), src).PathAndQuery;
                                                        src = String.Format("{0}/{1}{2}", this.WebResource, this.MD5(src), src);
                                                    }
                                                }
                                                break;

                                        }
                                    }

                                }
                                writer.Write(src);
                                writer.Write(bf[0]);
                                buffer.Clear();
                                break;


                            }
                            else
                            {
                                buffer.Add(bf[0]);
                                break;
                            }
                        }
                        writer.Write(buffer.ToArray());
                        writer.Write(bf[0]);
                        buffer.Clear();
                        break;

                    case ':':
                        if (isFind)
                        {
                            isPort = true;
                            if (isdomainUrl == false)
                            {
                                writer.Write(port2);
                            }
                        }
                        else
                        {
                            if (buffer.Count == bsize && isUrl == false && isUrlStart == false)
                            {
                                writer.Write(buffer[0]);
                                buffer.RemoveAt(0);
                            }

                            buffer.Add(bf[0]);

                        }
                        break;
                    case '/':
                        if (isFind && isdomainUrl == false)
                        {
                            writer.Write(port2);

                        }
                        goto default;
                    default:
                        if (isPort)
                        {
                            if (bf[0] > 47 && bf[0] < 58)
                            {
                                break;
                            }
                            else
                            {
                                isPort = false;
                            }
                        }
                        else if (isFind && bf[0] == '%')
                        {

                            isEncodePort = true;
                        }

                        if (buffer.Count == bsize && isUrl == false && isUrlStart == false)
                        {
                            writer.Write(buffer[0]);
                            buffer.RemoveAt(0);
                        }

                        buffer.Add(bf[0]);

                        if (isEncodePort && buffer.Count == 3)
                        {
                            isEncodePort = false;
                            var s = new String(buffer.ToArray());
                            if (String.Equals("%3A", s, StringComparison.CurrentCultureIgnoreCase))
                            {
                                isPort = true;

                                buffer.Clear();
                            }
                        }
                        break;
                }

                isFind = false;
                if (isUrl == false && isUrlStart == false)
                {
                    var hem = hosts.GetEnumerator();
                    while (hem.MoveNext())
                    {
                        var khost = hem.Current.Key;
                        var kvalue = hem.Current.Value;
                        if (EndsWith(buffer, khost))
                        {

                            isFind = true;
                            var isEncode = false;
                            buffer.RemoveRange(buffer.Count - khost.Length, khost.Length);
                            if (EndsWith(buffer, httpScheme))
                            {
                                isdomainUrl = true;
                                buffer.RemoveRange(buffer.Count - httpScheme.Length, httpScheme.Length);

                                writer.Write(buffer.ToArray());
                                writer.Write(kvalue[3]);

                            }
                            else if (EndsWith(buffer, httpsScheme))
                            {
                                isdomainUrl = true;
                                buffer.RemoveRange(buffer.Count - httpsScheme.Length, httpsScheme.Length);

                                writer.Write(buffer.ToArray());
                                writer.Write(kvalue[3]);

                            }
                            else if (EndsWith(buffer, encodeHttpScheme))
                            {
                                buffer.RemoveRange(buffer.Count - encodeHttpScheme.Length, encodeHttpScheme.Length);

                                writer.Write(buffer.ToArray());
                                writer.Write(Uri.EscapeDataString(kvalue[3]));
                                isEncode = true;

                            }
                            else if (EndsWith(buffer, encodeHttpsScheme))
                            {
                                buffer.RemoveRange(buffer.Count - encodeHttpsScheme.Length, encodeHttpsScheme.Length);

                                writer.Write(buffer.ToArray());
                                writer.Write(Uri.EscapeDataString(kvalue[3]));
                                isEncode = true;

                            }
                            else
                            {
                                writer.Write(buffer.ToArray());
                            }

                            if (isEncode)
                            {

                                writer.Write(Uri.EscapeDataString(kvalue[1]));
                            }
                            else
                            {
                                writer.Write(kvalue[1]);
                            }
                            if (isEncode)
                            {
                                isdomainUrl = true;
                                writer.Write(Uri.EscapeDataString(kvalue[2]));
                            }
                            else if (isdomainUrl)
                            {
                                writer.Write(kvalue[2]);

                            }
                            else
                            {
                                isdomainUrl = EndsWith(buffer, "//");
                                if (isdomainUrl)
                                {
                                    writer.Write(kvalue[2]);
                                }
                            }
                            buffer.Clear();
                            break;
                        }
                    }
                }
            }

            writer.Write(buffer.ToArray());
            writer.Flush();
            return ms;
        }
        bool EndsWith(List<Char> buffer, String end)
        {
            var el = end.Length;
            var bl = buffer.Count;
            if (el <= bl)
            {
                for (var i = 0; i < el; i++)
                {
                    if (end[el - 1 - i] != buffer[bl - 1 - i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
        Stream OuterCSS(Stream response, string encoding)
        {
            var ms = new System.IO.MemoryStream();
            var writer = new System.IO.StreamWriter(DataFactory.Instance().Compress(ms, encoding));
            var reader = new System.IO.StreamReader(DataFactory.Instance().Decompress(response, encoding));

            var webResource = this.WebResource;

            if (webResource.IndexOf(':') > 1)
            {
                webResource = webResource.Substring(webResource.IndexOf('/', 8));
            }
            var bf = new Char[1];
            var isFind = false;
            var isUrlStart = false;

            var buffer = new List<Char>();
            while (reader.ReadBlock(bf, 0, 1) > 0)
            {
                switch (bf[0])
                {
                    case ' ':
                        buffer.Add(bf[0]);

                        break;
                    case '(':

                        if (isFind)
                        {
                            isUrlStart = true;
                        }
                        writer.Write(buffer.ToArray());
                        writer.Write(bf[0]);
                        buffer.Clear();
                        break;
                    case ')':
                        if (isUrlStart)
                        {
                            isFind = false;

                            isUrlStart = false;

                            var src = WebUtility.HtmlDecode(new string(buffer.ToArray())).Trim('\'', '"');

                            if ((src.StartsWith("//") == false) && src.StartsWith("/"))
                            {
                                writer.Write('"');
                                writer.Write(String.Format("{0}/{1}{2}", webResource, this.MD5(src), src));

                                writer.Write('"');

                            }
                            else
                            {
                                writer.Write(buffer.ToArray());
                            }
                            buffer.Clear();
                            writer.Write(bf[0]);
                        }
                        else
                        {
                            writer.Write(buffer.ToArray());
                            writer.Write(bf[0]);
                            buffer.Clear();
                            isFind = false;
                        }
                        break;
                    default:
                        if (buffer.Count > 3 && isUrlStart == false)
                        {
                            writer.Write(buffer[0]);
                            buffer.RemoveAt(0);
                        }

                        buffer.Add(bf[0]);
                        if (buffer.Count >= 3 && isFind == false)
                        {
                            if (buffer[buffer.Count - 1] == 'l'
                                 && buffer[buffer.Count - 2] == 'r'
                                 && buffer[buffer.Count - 3] == 'u')
                            {
                                isFind = true;
                            }
                        }
                        break;

                }
            }
            writer.Write(buffer.ToArray());




            writer.Flush();
            return ms;
        }
    }
}
