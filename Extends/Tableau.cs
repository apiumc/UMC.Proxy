
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Linq;
using UMC.Web;
using UMC.Net;

namespace UMC.Proxy.Extends
{


    public class Tableau : Proxy.SiteProxy
    {

        string encryptedPassword(Hashtable hashtable, String passwrod)
        {
            var key = hashtable["key"] as Hashtable;
            return UMC.Data.Utility.Hex(UMC.Data.Utility.RSA(key["n"] as string, key["e"] as string, passwrod));
        }
        //Guid AppKey;
        public override string[] OutputCookies => new string[] { "XSRF-TOKEN" };
        public override bool Proxy(HttpProxy proxy)
        {
            //this.AppKey = UMC.Security.Principal.Current.AppKey ?? Guid.Empty;
            if (proxy.RawUrl.StartsWith("/t/"))
            {
                Chart(proxy);

                return true;
            }
            else if (proxy.RawUrl.StartsWith("/vizportal/api/web/v1/getSessionInfo"))
            {
                getSessionInfo(proxy);
                return true;
            }
            return false;
        }
        void Login(HttpProxy proxy, Action callback)
        {

            var webReq = proxy.Reqesut(new Uri(proxy.Domain, "/vizportal/api/web/v1/generatePublicKey").WebRequest(proxy.Cookies));
            webReq.Post(new WebMeta().Put("method", "generatePublicKey").Put("params", new WebMeta()), responseMessage =>
            {
                responseMessage.ReadAsString(kko =>
                {
                    var result = UMC.Data.JSON.Deserialize(kko) as Hashtable;
                    var publicKey = result["result"] as Hashtable;
                    var Value = new WebMeta();
                    //UMC.Security.Principal.Create(this.AppKey);
                    proxy.ShareUser();
                    Value.Put("username", proxy.SiteCookie.Account, "encryptedPassword", encryptedPassword(publicKey, proxy.Password), "keyId", publicKey["keyId"] as string);

                    var login = new WebMeta().Put("method", "login").Put("params", Value);

                    proxy.Reqesut(proxy.Context.Transfer(new Uri(proxy.Domain, "/vizportal/api/web/v1/login"), proxy.Cookies))
                             .Post(login, h =>
                             {
                                 h.ReadAsData((b, i, c) =>
                                 {
                                     if (b.Length == 0 && c == 0)
                                     {
                                         if (i == -1)
                                         {
                                             proxy.Context.Error(h.Error);
                                         }
                                         else
                                         {
                                             callback();
                                         }
                                     }
                                 });
                             });


                    proxy.IsChangeUser = true;

                }, proxy.Context.Error);
            });


        }
        void Chart(HttpProxy proxy)
        {

            if (proxy.RawUrl.Contains("Cache=NO"))
            {
                Login(proxy, () =>
                {
                    proxy.Reqesut(proxy.Context.Transfer(new Uri(proxy.Domain, proxy.RawUrl), proxy.Cookies)).Get(re => Chart(re, proxy)); ;
                });

            }
            else if (proxy.SiteCookie != null && (proxy.SiteCookie.LoginTime ?? 0) + 600 < UMC.Data.Utility.TimeSpan())
            {
                Login(proxy, () =>
                {
                    proxy.Reqesut(proxy.Context.Transfer(new Uri(proxy.Domain, proxy.RawUrl), proxy.Cookies)).Get(re => Chart(re, proxy)); ;
                });
            }
            else
            {
                proxy.Reqesut(proxy.Context.Transfer(new Uri(proxy.Domain, proxy.RawUrl), proxy.Cookies)).Get(re =>
                {
                    if (re.StatusCode == HttpStatusCode.Found)
                    {
                        Login(proxy, () =>
                        {
                            proxy.Reqesut(proxy.Context.Transfer(new Uri(proxy.Domain, proxy.RawUrl), proxy.Cookies)).Get(res => Chart(res, proxy)); ;
                        });
                    }
                    else
                    {
                        Chart(re, proxy);
                    }
                });

            }
        }
        void Chart(NetHttpResponse re, HttpProxy proxy)
        {
            re.ReadAsString(Result =>
            {
                var webResource = proxy.WebResource;
                var cdnKey = proxy.MD5(proxy.Site.Caption);

                var regex = new System.Text.RegularExpressions.Regex("(?<key>\\shref|\\ssrc)=\"(?<src>[^\"]+)\"");

                var bIndex = Result.IndexOf("<textarea ");
                if (bIndex == -1)
                {
                    proxy.Context.Output.Write(Result.Replace("\"/vizql/v_", String.Format("\"{0}/{1}/vizql/v_", webResource, cdnKey)));

                    proxy.Context.OutputFinish();
                    return;
                }
                var eIndex = Result.IndexOf("</textarea>");
                var bResult = Result.Substring(0, bIndex);
                var eResult = Result.Substring(eIndex);

                var textarea = Result.Substring(bIndex, eIndex - bIndex);
                textarea = textarea.Substring(textarea.IndexOf(">") + 1);
                textarea = WebUtility.HtmlDecode(textarea).Replace("\"/vizql/v_", String.Format("\"{0}/{1}/vizql/v_", webResource, cdnKey));

                bResult = regex.Replace(bResult, g =>
                {
                    var src = g.Groups["src"].Value;
                    if (src.IndexOf(':') == -1)
                    {
                        var vsrc = new Uri(proxy.Context.Url, WebUtility.HtmlDecode(src)).PathAndQuery;

                        src = String.Format("{0}/{1}{2}", webResource, cdnKey, vsrc);
                    }
                    return String.Format("{0}=\"{1}\"", g.Groups["key"], src);
                });
                eResult = regex.Replace(eResult, g =>
                {
                    var src = g.Groups["src"].Value;
                    if (src.IndexOf(':') == -1 && src.Length > 0)
                    {
                        var vsrc = new Uri(proxy.Context.Url, WebUtility.HtmlDecode(src)).PathAndQuery;

                        src = String.Format("{0}/{1}{2}", webResource, cdnKey, vsrc);
                    }
                    return String.Format("{0}=\"{1}\"", g.Groups["key"], src);
                });

                var sb = new StringBuilder();
                sb.Append(bResult);
                sb.Insert(bResult.LastIndexOf("</style>"), "#main-content{background-color: #fff}");

                sb.Append("<textarea id=\"tsConfigContainer\">");

                sb.Append(WebUtility.HtmlDecode(textarea));
                sb.Append(eResult);

                proxy.Context.ContentType = re.ContentType;
                proxy.Context.Output.Write(sb.ToString());
                proxy.Context.OutputFinish();
            }, proxy.Context.Error);

        }

        void getSessionInfo(HttpProxy proxy)
        {

            //var webReq = proxy.Context.Transfer(new Uri(proxy.Domain, "/vizportal/api/web/v1/getSessionInfo"), proxy.Cookies);
            var webReq = proxy.Reqesut(proxy.Context.Transfer(new Uri(proxy.Domain, "/vizportal/api/web/v1/getSessionInfo"), proxy.Cookies));
            var cookie = proxy.Cookies.GetCookie("XSRF-TOKEN");
            if (cookie != null)
            {
                webReq.Headers["X-XSRF-TOKEN"] = cookie.Value;
            }
            webReq.Post(new StringContent("{\"method\":\"getSessionInfo\",\"params\":{}}", UTF8Encoding.UTF8, "application/json"), re =>
            {
                switch (re.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        Login(proxy, () =>
                        {
                            cookie = proxy.Cookies.GetCookie("XSRF-TOKEN");
                            if (cookie != null)
                            {
                                webReq.Headers["X-XSRF-TOKEN"] = cookie.Value;
                            }

                            webReq.Post(new System.Net.Http.StringContent("{\"method\":\"getSessionInfo\",\"params\":{}}", UTF8Encoding.UTF8, "application/json"), res =>
                            {
                                res.Transfer(proxy.Context);
                            });
                        });
                        break;
                    default:
                        re.Transfer(proxy.Context);
                        break;
                }
            });

        }
    }

}

