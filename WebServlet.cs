using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using UMC.Data;
using UMC.Net;
using UMC.Security;
using UMC.Web;
using System.IO;
using System.Collections;
using UMC.Proxy.Entities;
using System.Net;

namespace UMC.Proxy
{
    public class WebServlet : UMC.Web.WebServlet
    {
        void IndexResource(Net.NetContext context)
        {
            context.ContentType = "text/html;charset=UTF-8";
            using (System.IO.Stream stream = typeof(Web.WebServlet).Assembly
                               .GetManifestResourceStream(String.Format("UMC.Data.Resources.header.html")))
            {
                context.Output.WriteLine(new System.IO.StreamReader(stream).ReadToEnd().Trim());

            }
            context.Output.WriteLine("    <script src=\"/UMC.Conf\"></script>");


            using (System.IO.Stream stream = typeof(Web.WebServlet).Assembly
                               .GetManifestResourceStream("UMC.Data.Resources.index.html"))
            {
                context.Output.WriteLine(new System.IO.StreamReader(stream).ReadToEnd());
            }
        }
        void Unauthorized(Net.NetContext context)
        {
            if (String.IsNullOrEmpty(context.QueryString["oauth_callback"]))
            {
                var reDomain = AuthDomain(context);
                context.Redirect(new Uri(reDomain, "/Unauthorized?oauth_callback=/").AbsoluteUri);
            }
            else
            {
                Unauthorized(context, UMC.Data.WebResource.Instance().Provider["title"] ?? "门神网关", context.QueryString["oauth_callback"]);
            }
        }
        void Close(Net.NetContext context)
        {

            context.StatusCode = 403;
            context.ContentType = "text/html";
            using (System.IO.Stream stream = typeof(WebServlet).Assembly
                               .GetManifestResourceStream(String.Format("UMC.Proxy.Resources.close.html")))
            {
                context.ContentLength = stream.Length;
                stream.CopyTo(context.OutputStream);
            }
        }
        void DocConf(Net.NetContext context, string key)
        {

            context.StatusCode = 200;
            context.ContentType = "text/javascript";
            context.Output.WriteLine(@"UMC.UI.Config({'posurl': 'https://ali.365lu.cn/UMC/" + context.Cookies["device"] + "' });");
            context.Output.WriteLine(@"UMC(function ($) {");
            context.Output.Write($"$.UI.Command('Subject', 'Menu', '{key}' ,");
            context.Output.WriteLine(@"function (xhr) {$.UI.On('Portfolio.List', xhr);});");
            context.Output.WriteLine(@"})");
        }
        void Desktop(Net.NetContext context, string key)
        {

            context.StatusCode = 200;
            context.ContentType = "text/html";
            using (System.IO.Stream stream = typeof(WebServlet).Assembly
                               .GetManifestResourceStream($"UMC.Proxy.Resources.{key}.html"))
            {
                context.ContentLength = stream.Length;
                UMC.Data.Utility.Copy(stream, context.OutputStream);
            }
        }
        void NotSupport(Net.NetContext context)
        {

            context.StatusCode = 401;
            context.ContentType = "text/html";
            using (System.IO.Stream stream = typeof(WebServlet).Assembly
                                     .GetManifestResourceStream("UMC.Proxy.Resources.Auth.nosupport.html"))
            {
                context.ContentLength = stream.Length;
                UMC.Data.Utility.Copy(stream, context.OutputStream);
            }
        }

        void CheckAuth(Net.NetContext context, string wk)
        {
            var account = UMC.Data.Reflection.Configuration("account");
            var appids = new List<String>();
            for (var i = 0; i < account.Count; i++)
            {
                var p = account[i];
                if (String.Equals(p.Type, wk))
                {
                    if (String.IsNullOrEmpty(p["appid"]) == false)
                    {
                        appids.Add(p["appid"]);

                    }
                }
            }
            switch (wk)
            {
                case "wxwork":
                    context.ContentType = "text/html; charset=UTF-8";
                    using (System.IO.Stream stream = typeof(WebServlet).Assembly//UMC.Proxy
                                             .GetManifestResourceStream("UMC.Proxy.Resources.Auth.wxwork.html"))
                    {
                        var str = new System.IO.StreamReader(stream).ReadToEnd();
                        var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                        {
                            var key = g.Groups["key"].Value.ToLower();
                            switch (key)
                            {
                                case "appid":
                                    return appids[0];

                            }
                            return "";

                        });
                        context.Output.Write(v);

                    }
                    break;
                case "dingtalk":

                    context.ContentType = "text/html; charset=UTF-8";
                    using (System.IO.Stream stream = typeof(WebServlet).Assembly//UMC.Proxy
                                             .GetManifestResourceStream("UMC.Proxy.Resources.Auth.dingtalk.html"))
                    {
                        var str = new System.IO.StreamReader(stream).ReadToEnd();
                        var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                        {
                            var key = g.Groups["key"].Value.ToLower();
                            switch (key)
                            {
                                case "appids":
                                    return UMC.Data.JSON.Serialize(appids);

                            }
                            return "";

                        });
                        context.Output.Write(v);

                    }
                    break;
            }

        }
        void HostModelPage(Net.NetContext context, String url)
        {
            context.AddHeader("Cache-Control", "no-store");
            context.ContentType = "text/html; charset=UTF-8";
            using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                  .GetManifestResourceStream("UMC.Proxy.Resources.login-html.html"))
            {
                var str = new System.IO.StreamReader(stream).ReadToEnd();
                context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                {
                    var key = g.Groups["key"].Value.ToLower();
                    switch (key)
                    {

                        case "title":
                            return "登录方式已升级";
                        case "html":
                            return $"<div class=\"umc-proxy-acounts\" style=\"margin-left: 60px;\"><a href=\"/HostModel\">老方式登录</a><a href=\"{url}\">用扫码登录</a></div>"

                            + $"<div style=\"color: #999; line-height: 50px; text-align: center;\">{UMC.Data.WebResource.Instance().Provider["title"] ?? "门神网关"}推荐使用“用扫码登录”，安全简便。</div>";

                    }
                    return "";

                }));

            }

        }
        void Auth(Net.NetContext context)
        {
            switch (context.HttpMethod)
            {
                case "POST":

                    var ns = new NameValueCollection(context.Form);
                    var sign = ns["umc-request-sign"];
                    ns.Remove("umc-request-sign");
                    if (String.IsNullOrEmpty(sign) == false)
                    {
                        if (String.Equals(Utility.Sign(ns, Data.WebResource.Instance().AppSecret()), sign, StringComparison.CurrentCultureIgnoreCase))
                        {

                            var srole = ns["umc-request-user-role"];
                            String[] roles = new string[0];
                            if (String.IsNullOrEmpty(srole) == false)
                            {
                                roles = srole.Split(',');
                            }
                            var id = ns["umc-request-user-id"];
                            var name = ns["umc-request-user-name"];
                            var alias = ns["umc-request-user-alias"];
                            var appName = ns["umc-request-app"] ?? "default";
                            if (String.IsNullOrEmpty(name) == false)
                            {
                                var sid = Data.Utility.Guid(id) ?? Utility.Guid(name, true).Value;
                                var user = UMC.Security.Identity.Create(sid, name, alias ?? name, roles);
                                AccessToken accessToken = AccessToken.Create(user, sid, "API/Auth", 30 * 60);

                                var sessionKey = UMC.Data.Utility.Guid(Data.Utility.Guid(String.Format("{0}{1}", appName, name), true).Value);

                                var session = new Session<Security.AccessToken>(accessToken, sessionKey);
                                session.Post(user, "API/Auth");
                                UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("device", sessionKey), context.Output);
                            }
                            else
                            {
                                context.StatusCode = 403;
                                UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", "缺少必要参数"), context.Output);
                            }
                        }
                        else
                        {
                            context.StatusCode = 403;
                            UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", "签名不正确"), context.Output);

                        }
                        return;
                    }

                    break;
            }

            var ua = context.UserAgent.ToUpper();
            if (ua.Contains("WXWORK") || ua.Contains("MICROMESSENGER"))
            {

                var account = UMC.Data.Reflection.Configuration("account");
                var appids = new List<String>();
                for (var i = 0; i < account.Count; i++)
                {
                    var p = account[i];
                    if (String.Equals(p.Type, "wxwork"))
                    {
                        if (String.IsNullOrEmpty(p["appid"]) == false)
                        {
                            appids.Add(p["appid"]);

                        }
                    }
                }
                if (appids.Count == 0)
                {
                    context.Redirect("/notsupport");
                    return;
                }
                else
                {

                    int valueIndex = context.RawUrl.IndexOf("?");
                    if (valueIndex > 0)
                    {
                        var value = context.RawUrl.Substring(valueIndex + 1);
                        WebResource.Instance().Push(UMC.Data.Utility.Guid(value, true).Value
                            , new WebMeta().Put("msg", "已经扫码成功"));
                        context.Redirect(String.Format("https://open.weixin.qq.com/connect/oauth2/authorize?appid={1}&response_type=code&scope=snsapi_base&state={2}&redirect_uri={0}#wechat_redirect", Uri.EscapeUriString(new Uri(AuthDomain(context), "/Auth/wxwork").AbsoluteUri), appids[0], value));
                    }


                }
            }
            else if (ua.Contains("DINGTALK"))
            {
                var account = UMC.Data.Reflection.Configuration("account");
                var appids = new List<String>();
                for (var i = 0; i < account.Count; i++)
                {
                    var p = account[i];
                    if (String.Equals(p.Type, "dingtalk"))
                    {
                        if (String.IsNullOrEmpty(p["appid"]) == false)
                        {
                            appids.Add(p["appid"]);

                        }
                    }
                }
                if (appids.Count == 0)
                {
                    context.Redirect("/notsupport");
                }
                else
                {
                    int valueIndex = context.RawUrl.IndexOf("?");
                    if (valueIndex > 0)
                    {
                        var value = context.RawUrl.Substring(valueIndex + 1);
                        WebResource.Instance().Push(UMC.Data.Utility.Guid(value, true).Value
                            , new WebMeta().Put("msg", "已经扫码成功"));
                    }

                    context.Redirect("/Auth/dingtalk" + context.Url.Query);

                }
            }
            else
            {
                context.Redirect("/notsupport");
            }

        }

        static Uri AuthDomain(Net.NetContext context)
        {
            var Domain = UMC.Proxy.DataFactory.Instance().WebDomain();

            if (String.IsNullOrEmpty(Domain) == false && Domain.StartsWith("/") == false)
            {

                return new Uri(String.Format("{0}://{1}", context.Url.Scheme, Domain));
            }
            else
            {
                return new Uri(context.Url, "/");
            }
        }
        void Unauthorized(Net.NetContext context, String title)
        {
            Unauthorized(context, title, context.RawUrl);
        }
        static void Unauthorized(Net.NetContext context, String title, string oauth_callback)
        {

            UMC.Net.NetContext.Authorization(context);
            var webr = UMC.Data.WebResource.Instance();

            var reDomain = AuthDomain(context);

            if (String.Equals(context.Url.Host, reDomain.Host) == false)
            {
                oauth_callback = context.Url.AbsoluteUri;
            }

            if (String.IsNullOrEmpty(context.UserAgent) == false)
            {
                var ua = context.UserAgent.ToUpper();
                if (ua.Contains("WXWORK"))
                {
                    var account = UMC.Data.Reflection.Configuration("account");
                    var appids = new List<String>();
                    for (var i = 0; i < account.Count; i++)
                    {
                        var p = account[i];
                        if (String.Equals(p.Type, "wxwork"))
                        {
                            if (String.IsNullOrEmpty(p["appid"]) == false)
                            {
                                appids.Add(p["appid"]);

                            }
                        }
                    }
                    if (appids.Count == 0)
                    {
                        Error(context, "企业微信提示", "缺少企业微信参数，请联系管理员", "");

                        return;
                    }
                    else
                    {
                        UMC.Security.AccessToken.Current.Put("oauth_callback", oauth_callback).Commit();//] as string;

                        context.Redirect(String.Format("https://open.weixin.qq.com/connect/oauth2/authorize?appid={1}&response_type=code&scope=snsapi_base&state={1}&redirect_uri={0}#wechat_redirect", Uri.EscapeDataString(new Uri(reDomain, "/wxwork").AbsoluteUri), appids[0]));
                        return;
                    }

                }
                else if (ua.Contains("UMC CLIENT"))
                {
                    context.StatusCode = 401;
                    context.AddHeader("Cache-Control", "no-store");
                    context.ContentType = "text/html; charset=UTF-8";
                    using (System.IO.Stream stream = typeof(WebServlet).Assembly
                                             .GetManifestResourceStream("UMC.Proxy.Resources.umc.html"))
                    {
                        context.ContentLength = stream.Length;
                        UMC.Data.Utility.Copy(stream, context.OutputStream);

                    }

                }
                else if (ua.Contains("MICROMESSENGER"))
                {
                    var account = UMC.Data.Reflection.Configuration("account");
                    var appids = new List<String>();
                    var wkappids = new List<String>();
                    for (var i = 0; i < account.Count; i++)
                    {
                        var p = account[i];
                        if (String.IsNullOrEmpty(p["appid"]) == false)
                        {
                            switch (p.Type)
                            {
                                case "weixin":
                                    appids.Add(p["appid"]);
                                    break;
                                case "wxwork":
                                    wkappids.Add(p["appid"]);
                                    break;
                            }
                        }
                    }


                    if (appids.Count == 0)
                    {
                        if (wkappids.Count > 0)
                        {
                            UMC.Security.AccessToken.Current.Put("oauth_callback", oauth_callback).Commit();//] as string;
                            context.Redirect(String.Format("https://open.weixin.qq.com/connect/oauth2/authorize?appid={1}&response_type=code&scope=snsapi_base&state={1}&redirect_uri={0}#wechat_redirect", Uri.EscapeDataString(new Uri(reDomain, "/wxwork").AbsoluteUri), wkappids[0]));

                        }
                        else
                        {
                            Error(context, "微信使用提示", "缺少微信参数，请联系管理员", "");
                            //context.Redirect("/Unauthorized");
                        }
                    }
                    else
                    {
                        UMC.Security.AccessToken.Current.Put("oauth_callback", oauth_callback).Commit();//] as string;
                        context.Redirect(String.Format("https://open.weixin.qq.com/connect/oauth2/authorize?appid={1}&response_type=code&scope=snsapi_base&state={1}&redirect_uri={0}#wechat_redirect", Uri.EscapeDataString(new Uri(reDomain, "/weixin").AbsoluteUri), appids[0]));
                    }
                }
                else if (ua.Contains("DINGTALK"))
                {
                    var account = UMC.Data.Reflection.Configuration("account");
                    var appids = new List<String>();
                    for (var i = 0; i < account.Count; i++)
                    {
                        var p = account[i];
                        if (String.Equals(p.Type, "dingtalk"))
                        {
                            if (String.IsNullOrEmpty(p["appid"]) == false)
                            {
                                appids.Add(p["appid"]);

                            }
                        }
                    }
                    if (appids.Count == 0)
                    {
                        Error(context, "钉钉使用提示", "缺少钉钉参数，请联系管理员", "");

                    }
                    else
                    {

                        if (String.Equals(context.Url.Host, reDomain.Host) == false)
                        {
                            context.Redirect(new Uri(reDomain, String.Format("/Unauthorized?oauth_callback={0}", Uri.EscapeDataString(oauth_callback))).AbsoluteUri);

                        }
                        else
                        {
                            context.StatusCode = 401;
                            context.AddHeader("Cache-Control", "no-store");
                            context.ContentType = "text/html; charset=UTF-8";
                            using (System.IO.Stream stream = typeof(WebServlet).Assembly//UMC.Proxy
                                                     .GetManifestResourceStream("UMC.Proxy.Resources.dingtalk.html"))
                            {
                                var str = new System.IO.StreamReader(stream).ReadToEnd();
                                var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                                {
                                    var key = g.Groups["key"].Value.ToLower();
                                    switch (key)
                                    {
                                        case "appids":
                                            return UMC.Data.JSON.Serialize(appids);
                                    }
                                    return "";

                                });
                                context.Output.Write(v);

                            }
                        }
                    }

                }
                else
                {
                    if (String.Equals(context.Url.Host, reDomain.Host) == false && context.Url.Host.EndsWith(reDomain.Host))
                    {
                        context.Redirect(new Uri(reDomain, String.Format("/Unauthorized?oauth_callback={0}", Uri.EscapeDataString(oauth_callback))).AbsoluteUri);

                    }
                    else
                    {
                        context.StatusCode = 401;
                        context.ContentType = "text/html; charset=UTF-8";
                        context.AddHeader("Cache-Control", "no-store");
                        using (System.IO.Stream stream = typeof(WebServlet).Assembly//UMC.Proxy
                                                    .GetManifestResourceStream("UMC.Proxy.Resources.pc.html"))
                        {
                            var str = new System.IO.StreamReader(stream).ReadToEnd();
                            var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                            {
                                var key = g.Groups["key"].Value.ToLower();
                                switch (key)
                                {
                                    case "title":
                                        return title;
                                }
                                return "";

                            });
                            context.Output.Write(v);

                        }
                    }

                }
            }
        }
        public static void Error(Net.NetContext context, String title, String msg, String log)
        {
            context.StatusCode = 401;
            context.ContentType = "text/html; charset=UTF-8";
            context.AddHeader("Cache-Control", "no-store");
            using (System.IO.Stream stream = typeof(WebServlet).Assembly//UMC.Proxy
                                        .GetManifestResourceStream("UMC.Proxy.Resources.error.html"))
            {
                var str = new System.IO.StreamReader(stream).ReadToEnd();
                var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                {
                    var key = g.Groups["key"].Value.ToLower();
                    switch (key)
                    {
                        case "title":
                            return title;
                        case "msg":
                            return msg;
                        case "log":
                            return log;

                    }
                    return "";

                });
                context.Output.Write(v);
                //context.Output.Flush();


            }
        }
        void Auth(Net.NetContext context, string wk)
        {
            context.ContentType = "text/html; charset=UTF-8";

            using (System.IO.Stream stream = typeof(WebServlet).Assembly//UMC.Proxy
                                                   .GetManifestResourceStream(String.Format("UMC.Proxy.Resources.{0}.html", wk)))
            {

                switch (wk)
                {
                    case "dingtalk":
                        {
                            var account = UMC.Data.Reflection.Configuration("account");
                            var appids = new List<String>();
                            for (var i = 0; i < account.Count; i++)
                            {
                                var p = account[i];
                                if (String.Equals(p.Type, wk))
                                {
                                    if (String.IsNullOrEmpty(p["appid"]) == false)
                                    {
                                        appids.Add(p["appid"]);

                                    }
                                }
                            }
                            var str = new System.IO.StreamReader(stream).ReadToEnd();
                            var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                            {
                                var key = g.Groups["key"].Value.ToLower();
                                switch (key)
                                {
                                    case "appids":
                                        return UMC.Data.JSON.Serialize(appids);

                                }
                                return "";

                            });
                            context.Output.Write(v);
                        }
                        break;
                    default:
                        UMC.Data.Utility.Copy(stream, context.OutputStream);
                        break;
                }

            }
        }
        protected override bool CheckStaticFile()
        {
            return false;
        }
        bool isNewCookie;
        String InitCookie(NetContext context, String Domain)
        {

            var cookie = context.Cookies[Security.Membership.SessionCookieName];
            if (String.IsNullOrEmpty(cookie))
            {
                isNewCookie = true;
                var sessionKey = Utility.Guid(Guid.NewGuid());
                context.Cookies[Security.Membership.SessionCookieName] = sessionKey;
                var cdmn = Domain;
                cookie = sessionKey;

                var SameSite = "";
                if (context.UrlReferrer != null && context.UrlReferrer.Host.EndsWith(Domain) == false && String.Equals(context.Url.Scheme, "https"))
                {
                    SameSite = "SameSite=None; Secure; ";

                }
                var cookieStr = "device={0}; {3}Expires={1}; HttpOnly; Domain={2}; Path=/";
                if (String.IsNullOrEmpty(Domain) || String.Equals("/", Domain) || context.Url.Host.EndsWith(cdmn) == false)
                {
                    cookieStr = "device={0}; {3}HttpOnly; Path=/";
                }
                else
                {
                    var ds = cdmn.Split('.');
                    if (ds.Length > 2)
                    {
                        cdmn = ds[ds.Length - 2] + "." + ds[ds.Length - 1];
                    }
                }
                context.AddHeader("Set-Cookie", String.Format(cookieStr, sessionKey, DateTime.Now.AddYears(10).ToString("r"), cdmn, SameSite));



            }
            return cookie;

        }
        void Synchronize(NetContext context)
        {

            var rnew = new System.IO.StreamReader(context.InputStream);
            var value = rnew.ReadToEnd();


            var hsh = JSON.Deserialize<Hashtable>(value);
            if (hsh != null)
            {
                var p = WebResource.Instance();
                String appId = p.Provider["appId"];
                var point = hsh["point"] as string;



                var time = hsh["time"] as string;
                var type = hsh["type"] as string;

                System.Collections.Specialized.NameValueCollection nvs = new System.Collections.Specialized.NameValueCollection();

                nvs.Add("from", appId);
                nvs.Add("time", time);
                nvs.Add("point", point);
                nvs.Add("type", type);
                var secret = p.Provider["appSecret"];


                if (String.Equals(hsh["sign"] as string, UMC.Data.Utility.Sign(nvs, secret)))
                {
                    var v = UMC.Data.HotCache.Cache(type, hsh["value"] as Hashtable);
                    if (v != null)
                    {
                        context.ContentType = "application/json;charset=utf-8";
                        UMC.Data.JSON.Serialize(v, context.Output, "ts");
                        //context.OutputFinish();
                        return;
                    }
                }
            }
            context.StatusCode = 404;


        }
        void LocalResources(NetContext context, String path, bool check)
        {
            var file = Reflection.ConfigPath($"Static{path}");
            if (check)
            {
                if (System.IO.File.Exists(file))
                {
                    TransmitFile(context, file);
                    return;
                }
            }
            var url = new Uri($"https://www.365lu.cn{path}?v.05");


            url.WebRequest().Get(xhr =>
            {
                if (xhr.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var stream = UMC.Data.Utility.Writer(file, false);

                    xhr.ReadAsData((b, i, c) =>
                    {
                        if (c == 0 && b.Length == 0)
                        {
                            stream.Close();
                            TransmitFile(context, file);
                            context.OutputFinish();
                        }
                        else
                        {
                            stream.Write(b, i, c);
                        }
                    });
                }
                else
                {
                    context.StatusCode = 404;

                    context.OutputFinish();
                }
            });
            context.UseSynchronousIO(() => { });

        }

        public override void ProcessRequest(NetContext context)
        {


            if (String.IsNullOrEmpty(context.Headers.GetIgnore("X-Umc-Request-Id")) == false)
            {
                context.StatusCode = 405;
                Error(context, "安全审记", "存在依赖请求风险", "");
                return;
            }
            else
            {
                context.Headers["X-Umc-Request-Id"] = Guid.NewGuid().ToString();
            }
            var Domain = UMC.Proxy.DataFactory.Instance().WebDomain();

            var Path = context.Url.AbsolutePath;
            var site = context.Headers.GetIgnore("umc-proxy-site");
            switch (Path)
            {
                case "/HostModel":

                    context.AddHeader("Set-Cookie", "HostModel=Source; HttpOnly; Max-Age=300; Path=/");
                    context.Redirect(context.UrlReferrer.AbsoluteUri);
                    return;
                case "/UMC":
                    this.IndexResource(context);
                    return;
                case "/UMC.Conf":
                    context.AddHeader("Cache-Control", "no-store");
                    context.ContentType = "text/javascript;charset=utf-8";
                    context.Output.Write("UMC.UI.Config({posurl: '");
                    context.Output.Write("/UMC/");
                    context.Output.Write(InitCookie(context, Domain));
                    context.Output.Write("'");
                    if (String.IsNullOrEmpty(Domain) == false && Domain.StartsWith("/") == false)
                    {

                        if (Domain.Split('.').Length > 2)
                        {
                            context.Output.Write(",'domain':'{0}://{1}'", context.Url.Scheme, Domain);
                        }
                        else
                        {
                            var dsite = WebResource.Instance().Provider["default-site"];
                            if (String.IsNullOrEmpty(dsite))
                            {
                                context.Output.Write(",'domain':'{0}://{1}'", context.Url.Scheme, Domain);

                            }
                            else
                            {
                                context.Output.Write(",'domain':'{0}://{1}.{2}'", context.Url.Scheme, dsite, Domain);

                            }
                        }
                    }
                    context.Output.Write("});");
                    return;
                case "/js/UMC.Conf.js":
                    base.ProcessRequest(context);
                    return;
                case "/notsupport":
                    NotSupport(context);
                    return;
                case "/Unauthorized":
                    InitCookie(context, Domain);
                    Unauthorized(context);
                    return;
                case "/Auth":
                    Auth(context);
                    return;
                case "/Auth/weixin":
                    CheckAuth(context, "weixin");
                    return;
                case "/Auth/wxwork":
                    CheckAuth(context, "wxwork");
                    return;
                case "/Auth/dingtalk":
                    CheckAuth(context, "dingtalk");
                    return;
                case "/weixin":
                case "/dingtalk":
                case "/wxwork":
                    Auth(context, Path.Substring(1));
                    return;
                default:
                    if (String.IsNullOrEmpty(site))
                    {
                        if (Path.StartsWith("/UMC/"))
                        {
                            if (Path.StartsWith("/UMC/css/") || Path.StartsWith("/UMC/js/"))
                            {
                                LocalResources(context, Path.Substring(4), true);
                            }
                            else
                            {
                                base.ProcessRequest(context);
                            }
                            return;
                        }

                    }

                    break;
            }

            switch (context.Headers.GetIgnore("umc-client-pfm"))
            {
                case "root":
                    base.ProcessRequest(context);
                    return;
                case "sync":
                    Synchronize(context);
                    return; ;
                default:
                    if (Path.StartsWith("/!/"))
                    {
                        var sessionKey = Utility.Guid(InitCookie(context, Domain), true);

                        Path = Path.Substring(3);
                        var key = Path;
                        var keyIndex = Path.IndexOf('/');
                        if (keyIndex > 0)
                        {
                            key = Path.Substring(0, keyIndex);
                        }

                        if (sessionKey.HasValue)
                        {
                            var seesion = UMC.Data.DataFactory.Instance().Session(key);
                            if (seesion != null)
                            {
                                var Value = UMC.Data.JSON.Deserialize<UMC.Security.AccessToken>(seesion.Content);
                                if (Value != null)
                                {
                                    var user = Value.Identity();

                                    var login = (UMC.Data.Reflection.Configuration("account") ?? new ProviderConfiguration())["login"] ?? Provider.Create("name", "name");
                                    var timeout = UMC.Data.Utility.IntParse(login.Attributes["timeout"], 3600);

                                    UMC.Security.AccessToken.Login(user, sessionKey.Value, timeout, "Desktop");
                                }

                                UMC.Data.DataFactory.Instance().Delete(seesion);


                            }
                        }

                        context.Redirect(String.Format("{0}{1}", Path.Substring(key.Length), context.Url.Query));
                        return;


                    }
                    else if (Path.StartsWith("/__CDN/"))
                    {
                        var keyIndex = Path.IndexOf('/', 8);
                        if (keyIndex > 0)
                        {
                            site = Path.Substring(7, keyIndex - 7);
                        }
                    }
                    break;
            }
            if (String.IsNullOrEmpty(site))
            {
                var host = context.Headers.GetIgnore("x-client-host") ?? context.Url.Host;
                if (String.IsNullOrEmpty(Domain) == false)
                {

                    if (host.EndsWith("-" + Domain))
                    {
                        site = host.Substring(0, host.IndexOf('-'));
                    }
                    else if (host.EndsWith("." + Domain))
                    {
                        site = host.Substring(0, host.IndexOf('.'));
                    }
                    else if (String.Equals(host, Domain) == false)
                    {
                        var hostSite = DataFactory.Instance().HostSite(host);
                        if (hostSite != null)
                        {
                            var siteConfig = UMC.Proxy.DataFactory.Instance().SiteConfig(hostSite.Root);
                            if (siteConfig.AllowAllPath == false)
                            {
                                var union = Data.WebResource.Instance().Provider["union"] ?? ".";
                                var scheme = Data.WebResource.Instance().Provider["scheme"] ?? "http";
                                var hostModel = siteConfig.Site.HostModel ?? HostModel.Select;
                                if (hostModel == HostModel.Disable)
                                {
                                    context.Redirect($"{scheme}://{hostSite.Root}{union}{Domain}{ context.RawUrl}");
                                    return;
                                }
                                switch (context.HttpMethod)
                                {
                                    case "GET":
                                        switch (hostModel)
                                        {
                                            case HostModel.Check:

                                                if (String.IsNullOrEmpty(context.Headers.GetIgnore("accept-language")) == false
                                                    && String.IsNullOrEmpty(context.Headers.GetIgnore("accept-encoding")) == false)
                                                {
                                                    context.Redirect($"{scheme}://{hostSite.Root}{union}{Domain}{ context.RawUrl}");
                                                    return;
                                                }
                                                break;
                                            case HostModel.Login:

                                                if (HttpProxy.IsLoginPath(siteConfig, context.RawUrl))
                                                {
                                                    context.Redirect($"{scheme}://{hostSite.Root}{union}{Domain}{ context.RawUrl}");
                                                    return;
                                                }
                                                break;
                                            case HostModel.Select:

                                                if (HttpProxy.IsLoginPath(siteConfig, context.RawUrl))
                                                {
                                                    if (String.Equals(context.Cookies["HostModel"], "Source") == false)
                                                    {
                                                        context.Redirect($"{scheme}://{hostSite.Root}{union}{Domain}{ context.RawUrl}");
                                                        return;
                                                    }
                                                }
                                                break;
                                        }
                                        break;
                                }
                            }
                            var pem = siteConfig.SubSite.GetEnumerator();
                            while (pem.MoveNext())
                            {
                                var key = pem.Current.Key;
                                if (context.RawUrl.StartsWith(key))
                                {
                                    var v = pem.Current.Value;
                                    var site2 = UMC.Proxy.DataFactory.Instance().SiteConfig(v);
                                    if (site2 != null)
                                    {
                                        siteConfig = site2;
                                        break;
                                    }
                                }
                            }
                            Transfer(siteConfig, context);
                            return;

                        }
                    }
                }
                else
                {
                    var h = host.IndexOf('.');
                    if (h > 0)
                    {
                        site = host.Substring(0, h);
                    }

                }
            }
            switch (context.HttpMethod)
            {
                case "GET":

                    if (String.IsNullOrEmpty(site))
                    {
                        var staticFile = Reflection.ConfigPath(String.Format("Static{0}", String.Equals(Path, "/") ? "/index.html" : Path));
                        if (Path.StartsWith("/log/"))
                        {
                            Authorization(context);
                            System.Security.Principal.IPrincipal user = UMC.Security.Identity.Current;
                            if (user.IsInRole(UMC.Security.Membership.AdminRole) == false)
                            {
                                context.Redirect("/");
                                return;
                            }
                        }
                        if (System.IO.File.Exists(staticFile))
                        {
                            TransmitFile(context, staticFile);
                            return;
                        }
                        else if (Path.StartsWith("/Docs/"))
                        {
                            Desktop(context, "desktop.doc");
                            return;
                        }
                        else if (Path.StartsWith("/Setting/"))
                        {
                            Desktop(context, "desktop.umc");
                            return;
                        }
                        else if (String.Equals(Path, "/") || String.Equals(Path, "/Desktop"))
                        {
                            Desktop(context, "desktop");
                            return;

                        }
                        else if (String.Equals(Path, "/favicon.ico"))
                        {
                            context.StatusCode = 200;
                            context.ContentType = "image/x-icon";
                            using (System.IO.Stream stream = typeof(WebServlet).Assembly
                                               .GetManifestResourceStream("UMC.Proxy.Resources.favicon.ico"))
                            {
                                context.ContentLength = stream.Length;
                                UMC.Data.Utility.Copy(stream, context.OutputStream);
                            }
                            return;
                        }
                        else if (Path.StartsWith("/Desktop/"))
                        {
                            Desktop(context, "desktop.page");
                            return;

                        }
                        else if (String.Equals(Path, "/js/Docs.Conf.js"))
                        {
                            DocConf(context, "Proxy");
                            return;
                        }
                        else if (Path.StartsWith("/css/") || Path.StartsWith("/js/"))
                        {
                            LocalResources(context, Path, false);
                            return;
                        }
                        else if (Path.StartsWith("/v"))
                        {
                            var keyIndex = Path.IndexOf('/', 2);
                            if (keyIndex > 0)
                            {
                                var ver = Path.Substring(2, keyIndex - 2);

                                if (System.Text.RegularExpressions.Regex.IsMatch(ver, "[\\d\\.]+"))
                                {
                                    LocalResources(context, Path, false);
                                    return;
                                }

                            }
                        }
                    }
                    else if (String.Equals(Path, "/js/Docs.Conf.js"))
                    {
                        var psite = UMC.Proxy.DataFactory.Instance().SiteConfig(site);
                        if (psite != null)
                        {
                            DocConf(context, String.IsNullOrEmpty(psite.Site.HelpKey) ? "Proxy" : psite.Site.HelpKey);
                        }
                        else
                        {

                            DocConf(context, "Proxy");
                        }
                        return;
                    }
                    break;
            }

            if (String.IsNullOrEmpty(site) == false)
            {

                var psite = UMC.Proxy.DataFactory.Instance().SiteConfig(site);

                if (psite != null)
                {
                    Proxy(context, psite, Domain);
                }
                else if (String.IsNullOrEmpty(Domain) == false)
                {
                    context.Redirect(String.Format("{0}://{1}/", context.Url.Scheme, Domain));
                }
                else
                {
                    context.Redirect("/");
                }

            }
            else if (String.IsNullOrEmpty(Domain) == false)
            {
                context.Redirect(String.Format("{0}://{1}/", context.Url.Scheme, Domain));
            }
            else
            {
                context.Redirect("/");
            }
        }
        HttpWebRequest WebTransfer(SiteConfig siteConfig, UMC.Net.NetContext context)
        {
            var webR = new Uri(HttpProxy.WeightUri(siteConfig, context), context.RawUrl).WebRequest();
            if (siteConfig.HeaderConf.Count > 0)
            {
                var he = siteConfig.HeaderConf.GetEnumerator();
                while (he.MoveNext())
                {
                    var v = he.Current.Value;
                    switch (v)
                    {
                        case "HOST":
                            webR.Headers[he.Current.Key] = context.Url.Authority;
                            break;
                        case "SCHEME":
                            webR.Headers[he.Current.Key] = context.Url.Scheme;
                            break;
                        case "ADDRESS":
                            webR.Headers[he.Current.Key] = context.UserHostAddress;
                            break;
                        default:
                            webR.Headers[he.Current.Key] = v;
                            break;
                    }
                }
            }

            var Headers = context.Headers;
            for (var i = 0; i < Headers.Count; i++)
            {
                var k = Headers.GetKey(i);
                var v = Headers.Get(i);
                switch (k.ToLower())
                {
                    case "content-type":
                        webR.ContentType = v;
                        break;
                    case "content-length":
                    case "connection":
                    case "host":
                        break;
                    case "user-agent":
                        webR.UserAgent = v;
                        break;
                    default:
                        webR.Headers.Add(k, v);
                        break;
                }
            }

            var host2 = siteConfig.Site.Host;
            if (String.IsNullOrEmpty(host2) == false)
            {
                var port = webR.RequestUri.Port;
                if (String.Equals(host2, "*"))
                {
                    host2 = context.Url.Authority;
                }
                else
                {
                    switch (port)
                    {
                        case 80:
                        case 443:
                            break;
                        default:
                            host2 = String.Format("{0}:{1}", host2, port);
                            break;
                    }
                    var Referer = webR.Headers[HttpRequestHeader.Referer];
                    if (String.IsNullOrEmpty(Referer) == false)
                    {
                        webR.Headers[HttpRequestHeader.Referer] = String.Format("{0}://{1}{2}", webR.RequestUri.Scheme, host2, Referer.Substring(Referer.IndexOf('/', 8)));

                    }
                    var Origin = webR.Headers["Origin"];
                    if (String.IsNullOrEmpty(Origin) == false)
                    {
                        webR.Headers["Origin"] = String.Format("{0}://{1}/", webR.RequestUri.Scheme, host2);
                    }
                }
                webR.Headers[System.Net.HttpRequestHeader.Host] = host2;
            }
            else
            {
                webR.Headers[System.Net.HttpRequestHeader.Host] = context.Url.Authority;
            }
            return webR;
        }
        void Transfer(SiteConfig siteConfig, UMC.Net.NetContext context)
        {
            context.UseSynchronousIO(() => { });
            switch (context.HttpMethod)
            {
                case "GET":
                    if (HttpProxy.CheckStaticPage(siteConfig, context.Url.AbsolutePath) == 0)
                    {
                        string filename = UMC.Data.Reflection.ConfigPath(String.Format("Cache\\{0}\\{1}", siteConfig.Site.Root, HttpProxy.Int64MD5(context.Url.PathAndQuery + siteConfig.Site.Caption + siteConfig.Site.Version)));

                        var md5 = HttpProxy.MD5(filename, "");

                        if (HttpProxy.CheckCache(context, filename, md5))
                        {
                            context.OutputFinish();
                        }
                        else
                        {
                            var wr = WebTransfer(siteConfig, context);
                            wr.Net(context, xhr =>
                            {
                                if (xhr.StatusCode == HttpStatusCode.OK)
                                {
                                    var contentType = xhr.ContentType ?? String.Empty;

                                    var vIndex = contentType.IndexOf(';');
                                    if (vIndex > 0)
                                    {
                                        contentType = contentType.Substring(0, vIndex);
                                    }
                                    xhr.Headers["ETag"] = md5;
                                    xhr.Header(context);
                                    xhr.ReadAsStream(ms =>
                                    {
                                        HttpProxy.Cache(filename, ms, contentType, xhr.ContentEncoding);
                                        ms.Position = 0;
                                        context.ContentLength = ms.Length;
                                        ms.CopyTo(context.OutputStream);
                                        context.OutputFinish();
                                    }, context.Error);
                                }
                                else
                                {
                                    xhr.Transfer(context);
                                }

                            });

                        }
                        return;
                    }
                    break;
            }
            var webR = WebTransfer(siteConfig, context);
            webR.Net(context, xhr =>
            {
                xhr.Transfer(context);
            });
        }
        void Proxy(UMC.Net.NetContext context, SiteConfig psite, String Domain)
        {
            var pem = psite.SubSite.GetEnumerator();
            while (pem.MoveNext())
            {
                var key = pem.Current.Key;
                if (context.RawUrl.StartsWith(key))
                {
                    var v = pem.Current.Value;
                    var site2 = UMC.Proxy.DataFactory.Instance().SiteConfig(v);
                    if (site2 != null)
                    {
                        psite = site2;
                        break;
                    }
                }
            }

            if (psite.Site.Flag == -1)
            {
                Close(context);
                return;
            }
            else if (psite.AllowAllPath)
            {

                Transfer(psite, context);
                return;
            }

            var path = context.RawUrl.Split('?')[0];
            foreach (var d in psite.AllowPath)
            {
                var isAllowPath = false;
                int splitIndex = d.IndexOf('*');
                switch (splitIndex)
                {
                    case -1:
                        isAllowPath = d[0] == '/' ? path.StartsWith(d) : String.Equals(path, d);
                        break;
                    case 0:
                        isAllowPath = d.Length > 1 ? path.EndsWith(d.Substring(1)) : true;
                        break;
                    default:
                        if (splitIndex == d.Length - 1)
                        {
                            isAllowPath = path.StartsWith(d.Substring(0, d.Length - 1));
                        }
                        else
                        {
                            isAllowPath = path.StartsWith(d.Substring(0, splitIndex)) && path.EndsWith(d.Substring(splitIndex + 1));

                        }
                        break;

                }
                if (isAllowPath)
                {
                    Transfer(psite, context);
                    return;
                }
            }
            InitCookie(context, Domain);
            if (isNewCookie)
            {
                var sessionKey = UMC.Data.Utility.Guid(context.Cookies[Security.Membership.SessionCookieName], true).Value;
                var user2 = UMC.Security.Identity.Create(sessionKey, "?", String.Empty);
                UMC.Security.Principal.Create(user2, Security.AccessToken.Create(user2, sessionKey, "Client/" + context.UserHostAddress, 0));
            }
            else
            {
                UMC.Net.NetContext.Authorization(context);
            }


            var IsAuth = false;
            var user = UMC.Security.Identity.Current;
            System.Security.Principal.IPrincipal principal = user;
            switch (psite.Site.AuthType ?? Web.WebAuthType.User)
            {
                case Web.WebAuthType.Admin:
                    if (principal.IsInRole(UMC.Security.Membership.AdminRole) || psite.AdminConf.Contains(user.Name))
                    {
                        IsAuth = true;
                    }
                    break;
                default:
                case Web.WebAuthType.All:
                    IsAuth = true;
                    break;
                case Web.WebAuthType.Check:
                    if (UMC.Security.AuthManager.IsAuthorization(psite.Root))
                    {
                        IsAuth = true;
                    }
                    break;
                case Web.WebAuthType.UserCheck:
                    if (principal.IsInRole(UMC.Security.Membership.UserRole))
                    {
                        if (UMC.Security.AuthManager.IsAuthorization(psite.Root))
                        {
                            IsAuth = true;
                        }
                    }
                    break;
                case Web.WebAuthType.User:
                    if (principal.IsInRole(UMC.Security.Membership.UserRole))
                    {
                        IsAuth = true;
                    }
                    break;
                case Web.WebAuthType.Guest:
                    if (user.IsAuthenticated)
                    {
                        IsAuth = true;
                    }
                    break;
            }
            if (IsAuth)
            {
                var httpProxy = new HttpProxy(psite, context);
                if (httpProxy.Domain == null)
                {
                    Close(context);
                }
                else
                {
                    httpProxy.ProcessRequest();
                }
            }
            else if (user.IsAuthenticated)
            {
                Error(context, "安全审记", $"你的权限不足或者登录过期 <a href=\"{AuthDomain(context).AbsoluteUri}\">(从新登录)</a>", "请从标准入口登录");
            }
            else
            {
                Unauthorized(context, psite.Caption);
            }

        }
    }

}
