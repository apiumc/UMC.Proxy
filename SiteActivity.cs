using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Reflection;
using UMC.Web;
using UMC.Data.Entities;
using UMC.Web.UI;
using UMC.Proxy.Entities;
using System.Security.Cryptography;

namespace UMC.Proxy.Activities
{
    /// <summary>
    /// 应用管理
    /// </summary>
    [UMC.Web.Mapping("Proxy", "Site", Auth = WebAuthType.User)]
    class SiteActivity : WebActivity
    {
        public void Create()
        {

            if (DataFactory.Instance().IsRegister() == false)
            {
                this.Prompt("请先注册，再来新增应用");
            }

            var config = this.AsyncDialog("Config", g =>
            {
                var home = DataFactory.Instance().WebDomain();
                var union = Data.WebResource.Instance().Provider["union"] ?? ".";
                var from = new Web.UIFormDialog() { Title = "新增应用" };
                from.AddText("应用标识", "Root", String.Empty).PlaceHolder("[a-z0-9]全小写字符");
                from.AddText("应用名称", "Caption", String.Empty).Put("tip", $"将以*{union}{home}域名访问");
                from.AddText("应用网址", "Domain", String.Empty);
                from.AddCheckBox("", "AuthConf", "none").Put("仅作为反向代理", "*", true);
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });

            var site = new Site();
            site.Root = config["Root"].ToLower();
            if (System.Text.RegularExpressions.Regex.IsMatch(site.Root, "^[a-z0-9]+$") == false)
            {
                this.Prompt("应用标识只支持【a-z0-9】字符");
            }
            site.Caption = config["Caption"];
            var Domain = new Uri(config["Domain"]);
            var key = Domain.PathAndQuery.Substring(1);

            site.SiteKey = UMC.Data.Utility.IntParse(UMC.Data.Utility.Guid(site.Root, true).Value);

            site.Domain = new Uri(Domain, "/").AbsoluteUri;
            if (config["AuthConf"].Contains("*"))
            {
                site.AuthConf = "*";
                site.Host = "*";
            }
            site.AuthType = WebAuthType.All;
            site.IsDesktop = true;

            var oldSite = DataFactory.Instance().Site(site.Root);
            if (oldSite != null)
            {
                if (oldSite.Flag == -1)
                {
                    this.AsyncDialog("Confirm", g =>
                     {
                         var from = new Web.UIConfirmDialog("此标识存在于移除应用中，是否恢复此应用") { Title = "应用提示" };

                         return from;
                     });

                    DataFactory.Instance().Put(new Site { Root = site.Root, Flag = 0 });
                    this.Context.Send("Site.Config", true);
                }
                else
                {
                    this.Prompt("此应用标识已注册");
                }
            }
            else
            {
                DataFactory.Instance().Put(site);
            }
            this.Prompt("应用注册成功", false);
            this.Context.Send("Site.Config", true);
        }
        public void AdminConf(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "应用管理员" };
                from.AddTextarea("账户名称", "AdminConf", site.AdminConf);
                from.AddPrompt("多个用换行、空格或逗号符分割");
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            String OutputCookie = config["AdminConf"];
            if (OutputCookie == "none")
            {
                OutputCookie = String.Empty;
            }
            DataFactory.Instance().Put(new Site { AdminConf = OutputCookie, Root = site.Root });
            this.Context.Send("Site.Config", true);
        }
        public void OutputCookie(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "透传会话" };
                from.AddTextarea("Cookie名称", "OutputCookie", site.OutputCookies).PlaceHolder("Cookie的名称");
                from.AddPrompt("多个用换行、空格或逗号符分割，用*表示透传所有Cookie");
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            String OutputCookie = config["OutputCookie"];
            if (OutputCookie == "none")
            {
                OutputCookie = String.Empty;
            }
            DataFactory.Instance().Put(new Site { OutputCookies = OutputCookie, Root = site.Root });
            this.Context.Send("Site.Config", true);
        }
        public void Delete(Site site)
        {

            this.AsyncDialog("Config", g =>
             {
                 return new Web.UIConfirmDialog("您确认移除此应用吗") { Title = "移除提示" };
             });
            if (site.IsModule != true)
            {
                this.Prompt("请先把应用设置为隐藏应用，再来删除");
            }
            DataFactory.Instance().Put(new Site { Flag = -1, Root = site.Root });

            DataFactory.Instance().Delete(new SiteConfig { Root = site.Root });
            this.Context.Send("Site.Config", true);
        }

        public void Path(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "新增应用目录" };
                from.AddText("目录", "Path", String.Empty).PlaceHolder("以/开始的路径");
                from.AddText("应用", "Value", String.Empty);
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var Key = config["Path"];
            if (Key.StartsWith("/") == false)
            {
                this.Prompt("虚拟目录需要以/开头");
            }
            var Value = config["Value"];
            var site2 = DataFactory.Instance().Site(Value);
            if (site2 == null)
            {
                this.Prompt("未找到此标志的应用");
            }
            var path = new Hashtable();

            if (String.IsNullOrEmpty(site.Conf) == false)
            {
                var v = UMC.Data.JSON.Deserialize(site.Conf) as Hashtable;
                if (v != null)
                {
                    path = v;
                }
            }
            path[Key] = Value;
            site.Conf = UMC.Data.JSON.Serialize(path);
            DataFactory.Instance().Put(new Site
            {
                Root = site.Root,
                Conf = site.Conf
            });

            this.Context.Send("Site.Config", true);
        }
        void TimeOut(Site site)
        {
            var config = this.AsyncDialog("TimeOut", g =>
            {
                var from = new Web.UIFormDialog() { Title = "应用时效设置" };
                from.AddNumber("请求超时", "Timeout", site.Timeout).PlaceHolder("单位为秒，默认100秒");
                from.AddNumber("登录过期", "AuthExpire", site.AuthExpire).PlaceHolder("单位为分,默认30分钟");
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var Timeout = UMC.Data.Utility.IntParse(config["Timeout"], 100);
            var AuthExpire = UMC.Data.Utility.IntParse(config["AuthExpire"], 0);
            if (Timeout <= 0 || AuthExpire < 0)
            {
                this.Prompt("时间不能小于零");
            }

            DataFactory.Instance().Put(new Site { Root = site.Root, AuthExpire = AuthExpire, Timeout = Timeout });
            this.Context.Send("Site.Config", true);
        }
        void HeaderConf(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "请求配置" };
                from.AddTextarea("请求头配置", "HeaderConf", site.HeaderConf).PlaceHolder("字典配置格式").Put("Rows", 8);
                from.AddPrompt("将会追加请求的Header上，当值为HOST、SCHEME、ADDRESS将会分别替换成当前值");

                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var HostReConf = config["HeaderConf"];


            DataFactory.Instance().Put(new Site { Root = site.Root, HeaderConf = HostReConf });
            this.Context.Send("Site.Config", true);
        }
        void HostReConf(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "内容转化配置" };
                from.AddTextarea("转化路径", "HostReConf", site.HostReConf).PlaceHolder("字典配置格式").Put("Rows", 8);
                from.AddPrompt("值可为rp、rm、in、cdn、CDN，其中rp表示替换域名、rm表示移除域名、in表示提交内容域名转化，cdn表示静态资源加速，CDN表示以资源标签加速");

                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var HostReConf = config["HostReConf"];


            DataFactory.Instance().Put(new Site { Root = site.Root, HostReConf = HostReConf });
            this.Context.Send("Site.Config", true);
        }
        void LogoutPath(Site site)
        {
            var sValue = Web.UIDialog.AsyncDialog("LogoutPath", g =>
            {
                var from2 = new UIFormDialog() { Title = "触发登录页面" };
                from2.AddTextarea("触发登录页面", "LogoutPath", site.LogoutPath as string).Put("Rows", 5);

                from2.AddPrompt("结尾是“$”则表示从后比对，多项用换行、空格或逗号符分割");
                from2.Submit("确认", this.Context.Request, "Site.Config");
                return from2;
            });

            DataFactory.Instance().Put(new Site
            {
                LogoutPath = sValue,
                Root = site.Root
            });
            this.Context.Send("Site.Config", true);
        }
        void Home(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "应用设置" };
                from.AddText("应用名称", "Caption", site.Caption);
                from.AddText("应用主页", "Home", site.Home).NotRequired();
                //from.AddText("移动主页", "MobileHome", site.MobileHome).NotRequired();
                from.AddText("缓存版本", "Version", site.Version).NotRequired();


                var userBrowser = site.UserBrowser ?? Entities.UserBrowser.All;
                from.AddCheckBox("支持浏览器", "UserBrowser", "All")
                .Put("谷歌浏览器", "Chrome", (userBrowser & UserBrowser.Chrome) == UserBrowser.Chrome)
                .Put("IE浏览器", "IE", (userBrowser & UserBrowser.IE) == UserBrowser.IE)
                //.Put("Opera", "Opera", (userBrowser & UserBrowser.Opera) == UserBrowser.Opera)
                .Put("火狐浏览器", "Firefox", (userBrowser & UserBrowser.Firefox) == UserBrowser.Firefox)
                .Put("WebKit内核浏览器", "WebKit", (userBrowser & UserBrowser.WebKit) == UserBrowser.WebKit)
                .Put("钉钉", "Dingtalk", (userBrowser & UserBrowser.Dingtalk) == UserBrowser.Dingtalk)
                .Put("微信", "WeiXin", (userBrowser & UserBrowser.WeiXin) == UserBrowser.WeiXin);




                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var Home = config["Home"];
            if (String.IsNullOrEmpty(Home) == false)
            {
                if (Home.StartsWith("https://") == false && Home.StartsWith("http://") == false)
                {
                    if (Home.StartsWith("/") == false || Home.StartsWith("//"))
                    {
                        this.Prompt("主页格式不正确，请确认");
                    }
                }
            }
            else
            {
                Home = null;
            }

            var version = config["Version"];
            //UMC.Data.Utility
            if (String.IsNullOrEmpty(version) == false)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(version, "^[\\.0-9]+$") == false)
                {
                    this.Prompt("版本号只支持数字和点");
                }
            }
            else
            {
                version = null;
            }
            var confgiSite = new Site
            {
                Caption = config["Caption"],
                Home = Home,
                Version = version,
                //MobileHome = MobileHome,
                Root = site.Root
            };
            var userBrowser2 = UserBrowser.All;
            foreach (var v in config["UserBrowser"].Split(','))
            {
                userBrowser2 |= UMC.Data.Utility.Parse(v, UserBrowser.All);
            }
            confgiSite.UserBrowser = userBrowser2;
            DataFactory.Instance().Put(confgiSite);
            this.Context.Send("Site.Config", true);
        }
        public void Setting(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "应用设置" };


                from.AddRadio("授权", "AuthType")
                .Put("所有人", "All", site.AuthType == WebAuthType.All)
                .Put("登录人员", "Guest", site.AuthType == WebAuthType.Guest)
                .Put("内部用户", "User", site.AuthType == WebAuthType.User)
                .Put("管理员", "Admin", site.AuthType == WebAuthType.Admin);


                from.AddCheckBox("设置", "Setings", "0")
                .Put("桌面展示", "IsDesktop", site.IsDesktop == true)
                .Put("隐藏应用", "IsModule", site.IsModule == true)
                .Put("开启日志", "IsDebug", site.IsDebug == true);


                from.AddRadio("图标打开方式", "OpenModel")
                .Put("新窗口", "0", (site.OpenModel ?? 0) == 0)
                .Put("当前窗口", "1", site.OpenModel == 1)
                .Put("最大化窗口", "2", site.OpenModel == 2)
                .Put("快捷方式", "3", site.OpenModel == 3);


                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var confgiSite = new Site
            {
                //HelpKey = config["HelpKey"],
                OpenModel = UMC.Data.Utility.Parse(config["OpenModel"], 0),
                IsModule = false,
                IsDebug = false,
                IsDesktop = false,
                AuthType = UMC.Data.Utility.Parse(config["AuthType"], UMC.Web.WebAuthType.User),
                Root = site.Root
            };
            var sDoHtmlType = config["Setings"].Split(',');
            foreach (var v in sDoHtmlType)
            {
                switch (v)
                {
                    case "IsDesktop":
                        confgiSite.IsDesktop = true;
                        break;
                    case "IsModule":
                        confgiSite.IsModule = true;
                        break;
                    case "IsDebug":
                        confgiSite.IsDebug = true;
                        break;
                }
            }
            DataFactory.Instance().Put(confgiSite);
            this.Context.Send("Site.Config", true);
        }
        string MD5(String src)
        {
            var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] md = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(src));
            return UMC.Data.Utility.Parse36Encode(UMC.Data.Utility.IntParse(md));
        }
        public void Share(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "设置共享模式" };
                var m = "标准模式";
                switch ((site.UserModel ?? UserModel.Standard))
                {
                    case UserModel.Bridge:
                        m = "桥接模式";
                        break;
                    case UserModel.Share:
                        m = "共享模式";
                        break;
                    case UserModel.Quote:
                        m = "引用模式";
                        break;
                    case UserModel.Check:
                        m = "自主检测";
                        break;
                    case UserModel.Checked:
                        m = "自动检测";
                        break;
                }
                from.AddTextValue().Put("当前模式", m);

                from.AddText("共享账户", "Account", site.Account).PlaceHolder("多账户可用|和~分割");

                from.AddText("共同密码", "Password", String.Empty);
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var acount = config["Account"];
            var pwd = config["Password"];

            var site2 = new Site()
            {
                Root = site.Root,
                Account = acount,
                UserModel = UserModel.Share
            };
            if (String.IsNullOrEmpty(pwd) == false)
            {
                UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(site.Root, acount), pwd);
            }
            DataFactory.Instance().Put(site2);
            this.Context.Send("Site.Config", true);
        }
        public void SetHostModel(Site site)
        {
            var config = this.AsyncDialog("HostModel", g =>
            {
                var from = new Web.UISheetDialog() { Title = "域名反代方式" };
                var arg = this.Context.Request.Arguments;
                var m = this.Context.Request.Model;
                var c = this.Context.Request.Command;
                from.Options.Add(new UIClick(new WebMeta(arg).Put(g, "Select")) { Text = "登录页选择" }.Send(m, c));
                from.Options.Add(new UIClick(new WebMeta(arg).Put(g, "Login")) { Text = "登录页跳转" }.Send(m, c));
                from.Options.Add(new UIClick(new WebMeta(arg).Put(g, "Check")) { Text = "浏览器中跳转" }.Send(m, c));

                from.Options.Add(new UIClick(new WebMeta(arg).Put(g, "Disable")) { Text = "全域名跳转" }.Send(m, c));

                return from;
            });
            var acount = UMC.Data.Utility.Parse(config, HostModel.Select);
            DataFactory.Instance().Put(new Site
            {
                HostModel = acount,
                Root = site.Root
            });
            this.Context.Send("Site.Config", true);
        }
        public void UseModel(Site site)
        {
            var config = this.AsyncDialog("UserModel", g =>
            {
                var from = new Web.UISheetDialog() { Title = "设置账户模式" };
                var arg = this.Context.Request.Arguments;
                var m = this.Context.Request.Model;
                var c = this.Context.Request.Command;
                from.Options.Add(new UIClick(new WebMeta(arg).Put(g, "Standard")) { Text = "标准模式" }.Send(m, c));

                //from.Options.Add(new UIClick(new WebMeta(arg).Put(g, "More")) { Text = "多账户模式" }.Send(m, c));
                from.Options.Add(new UIClick(new WebMeta(arg).Put(g, "Bridge")) { Text = "桥接模式" }.Send(m, c));
                from.Options.Add(new UIClick(new WebMeta(arg).Put("Model", "Quote")) { Text = "引用模式" }.Send(m, c));
                from.Options.Add(new UIClick(new WebMeta(arg).Put("Model", "Share")) { Text = "共享模式" }.Send(m, c));


                return from;
            });
            var acount = UMC.Data.Utility.Parse(config, UserModel.Standard);
            DataFactory.Instance().Put(new Site
            {
                UserModel = acount,
                Root = site.Root
            });
            this.Context.Send("Site.Config", true);
        }
        public void Quote(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "设置引用模式" };
                var ak = "";
                if (String.IsNullOrEmpty(site.Account) == false && site.Account.StartsWith("@"))
                {
                    ak = site.Account.Substring(1);
                }
                var m = "标准模式";
                switch ((site.UserModel ?? UserModel.Standard))
                {
                    case UserModel.Bridge:
                        m = "桥接模式";
                        break;
                    //case UserModel.More:
                    //    m = "多账户模式";
                    //    break;
                    case UserModel.Share:
                        m = "共享模式";
                        break;
                    case UserModel.Quote:
                        m = "引用模式";
                        break;
                    case UserModel.Check:
                        m = "自主检测";
                        break;
                    case UserModel.Checked:
                        m = "自动检测";
                        break;
                }
                from.AddTextValue().Put("当前模式", m);

                from.AddText("引用应用", "Account", ak);
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var acount = config["Account"];

            var site2 = DataFactory.Instance().Site(acount);
            if (site2 == null)
            {
                this.Prompt("未有引用的应用");
            }
            if (site2.Root == site.Root)
            {
                this.Prompt("引用应用不能配置自己");
            }
            switch (site2.UserModel ?? UserModel.Standard)
            {
                case UserModel.Standard:
                case UserModel.Share:
                case UserModel.Checked:
                case UserModel.Check:
                    break;
                default:
                    this.Prompt("应用不支持引用对接");
                    break;
            }
            DataFactory.Instance().Put(new Site
            {
                Account = "@" + acount,
                UserModel = UserModel.Quote,
                Root = site.Root
            });
            this.Context.Send("Site.Config", true);
        }
        public void Webhook(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "异常机器人" };
                from.AddText("Webhook", "Webhook", site.Webhook);
                from.AddText("Secret", "WebhookSecret", site.WebhookSecret);
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var Webhook = config["Webhook"];
            var WebhookSecret = config["WebhookSecret"];

            var site2 = new Site()
            {
                Root = site.Root,
                Webhook = Webhook,
                WebhookSecret = WebhookSecret
            };

            DataFactory.Instance().Put(site2);

            this.Context.Send("Site.Config", true);
        }
        public void Account(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "检测账户" };
                from.AddText("账户名称", "Account", site.Account);
                from.AddText("账户密码", "Password", String.Empty).NotRequired();
                from.Submit("确认", this.Context.Request, "Mime.Config");
                return from;
            });
            var acount = config["Account"];
            var pwd = config["Password"];

            var site2 = new Site()
            {
                Root = site.Root,
                Account = acount
            };
            if (String.IsNullOrEmpty(pwd) == false)
            {
                UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(site.Root, acount), pwd);
            }
            DataFactory.Instance().Put(site2);

            this.Context.Send("Mime.Config", true);
        }
        void Copy(Site site)
        {

            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "复制应用" };

                from.AddText("新标识", "Root", String.Empty).PlaceHolder("[a-z0-9]全小写字符");

                from.AddText("新名称", "Caption", site.Caption);
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var newRoot = config["Root"].ToLower();
            if (System.Text.RegularExpressions.Regex.IsMatch(newRoot, "^[a-z0-9]+$") == false)
            {
                this.Prompt("应用标识只支持【a-z0-9】字符");
            }



            var oldSite = DataFactory.Instance().Site(newRoot);
            if (oldSite != null)
            {
                this.Prompt("此应用标识已注册");
            }


            CopyMime("SITE_MIME_{0}_LOGIN", site.Root, newRoot);
            CopyMime("SITE_MIME_{0}_CHECK", site.Root, newRoot);
            CopyMime("SITE_MIME_{0}_UPDATE", site.Root, newRoot);


            var jsPaths = SiteConfig.Config(site.AppendJSConf);
            foreach (var key in jsPaths)
            {
                var md5Key = MD5(key as string);
                var jsKey = String.Format("SITE_JS_CONFIG_{0}{1}", site.Root, md5Key).ToUpper();//).Send(request.Model, "Conf"));

                var pconfig = UMC.Data.DataFactory.Instance().Config(jsKey);
                if (pconfig != null)
                {
                    pconfig.ConfKey = String.Format("SITE_JS_CONFIG_{0}{1}", newRoot, md5Key).ToUpper();//).Send(request.Model, "Conf"));
                    UMC.Data.DataFactory.Instance().Put(pconfig);
                }
            }

            var htmlconfig = UMC.Data.DataFactory.Instance().Config($"SITE_MIME_{site.Root}_LOGIN_HTML".ToUpper());
            if (htmlconfig != null)
            {
                htmlconfig.ConfKey = $"SITE_MIME_{newRoot}_LOGIN_HTML".ToUpper();
                UMC.Data.DataFactory.Instance().Put(htmlconfig);
            }

            site.Root = newRoot;
            site.Caption = config["Caption"];
            site.SiteKey = UMC.Data.Utility.IntParse(UMC.Data.Utility.Guid(newRoot, true).Value);
            DataFactory.Instance().Put(site);

            this.Prompt("应用复制成功", false);
            this.Context.Send("Site.Config", true);
        }
        void CopyMime(String formt, String old, String root)
        {

            var oldKey = String.Format(formt, old).ToUpper();
            var newKey = String.Format(formt, root).ToUpper();

            var pconfig = UMC.Data.DataFactory.Instance().Config(oldKey);
            if (pconfig == null)
            {
                return;

            }
            var config = new Hashtable();
            var v = UMC.Data.JSON.Deserialize(pconfig.ConfValue) as Hashtable;
            if (v != null)
            {
                config = v;
            }
            pconfig.ConfKey = newKey;
            UMC.Data.DataFactory.Instance().Put(pconfig);

            var feilds = config["Feilds"] as Hashtable;
            if (feilds != null && feilds.Count > 0)
            {
                var fd = feilds.Keys.Cast<String>().OrderBy(r => r).GetEnumerator();

                while (fd.MoveNext())
                {

                    var fconfig = UMC.Data.DataFactory.Instance().Config($"{oldKey}_{fd.Current}".ToUpper());
                    if (fconfig != null)
                    {
                        fconfig.ConfKey = $"{newKey}_{fd.Current}".ToUpper();
                        UMC.Data.DataFactory.Instance().Put(fconfig);
                    }

                }

            }

        }

        public override void ProcessActivity(WebRequest request, WebResponse response)
        {

            var Key = this.AsyncDialog("Key", g =>
            {

                var sts = new System.Data.DataTable();
                sts.Columns.Add("id");
                //sts.Columns.Add("icon");
                sts.Columns.Add("name");
                sts.Columns.Add("root");
                sts.Columns.Add("domain");
                sts.Columns.Add("module");
                sts.Columns.Add("auth");
                var keys = new List<String>();
                var ds = DataFactory.Instance().Site();

                var Keyword = (request.SendValues ?? request.Arguments)["Keyword"];
                if (String.IsNullOrEmpty(Keyword) == false)
                {

                    ds = ds.Where(r => r.Caption.Contains(Keyword) || r.Root.Contains(Keyword) || r.Domain.Contains(Keyword)).Where(r => r.Flag != -1).OrderBy(r => r.Caption).ToArray();
                }
                else
                {

                    ds = ds.Where(r => r.Flag != -1).OrderBy(r => r.Caption).ToArray();
                }
                foreach (var d in ds)
                {
                    var dtype = "桌面展示";
                    if (d.IsModule == true)
                    {
                        dtype = "应用隐藏";
                    }
                    else if (d.IsDesktop ?? false == false)
                    {
                        dtype = "桌面展示";
                    }
                    var Domain = d.Domain ?? ""; ;
                    sts.Rows.Add(d.SiteKey ?? UMC.Data.Utility.IntParse(UMC.Data.Utility.Guid(d.Root, true).Value), d.Caption, d.Root, (Domain.IndexOf(',') > 0 || Domain.IndexOf('\n') > 0) ? "多例均衡" : Domain, dtype, d.AuthType ?? Web.WebAuthType.All);

                }

                var rdata = new WebMeta().Put("data", sts);
                response.Redirect(request.IsMaster ? rdata.Put("IsMaster", true) : rdata);
                return this.DialogValue("none");
            });
            if (Key == "Create")
            {
                if (request.IsMaster == false)
                {
                    this.Prompt("新建应用需要管理员权限");
                }
                this.Create();
                return;
            }
            var site = DataFactory.Instance().Site(Key);
            var ms = request.SendValues ?? request.Arguments;
            var Model = this.AsyncDialog("Model", g =>
            {
                if (ms.ContainsKey("limit") == false)
                {
                    this.Context.Send(new UISectionBuilder(request.Model, request.Command, request.Arguments)
                        .RefreshEvent("Site.Config", "image")
                        .Builder(), true);
                }

                var title = UITitle.Create();

                title.Title = "应用配置";
                if (request.IsMaster)
                {
                    title.Right(new UIEventText("复制").Click(new UIClick(new WebMeta(request.Arguments).Put(g, "Copy")).Send(request.Model, request.Command)));

                }

                var ui = UISection.Create(title);
                var imageId = Data.Utility.Guid(site.Root, true);

                var imageTextView = new UMC.Web.UI.UIImageTextValue(Data.WebResource.Instance().ImageResolve(imageId.Value, "1", 4), "", "图标");
                imageTextView.Style.Name("image-width", "100");
                imageTextView.Click(new UIClick("id", imageId.ToString(), "seq", "1")
                { Model = "Design", Command = "Picture" });

                ui.Add(imageTextView);

                ui.AddCell("应用标识", site.Root, new UIClick(new WebMeta(request.Arguments).Put(g, "Setting")).Send(request.Model, request.Command))

                 .AddCell("应用名称", site.Caption, new UIClick(new WebMeta(request.Arguments).Put(g, "Home")).Send(request.Model, request.Command));




                ui.NewSection().AddCell("负载网址", (site.Domain.IndexOf(',') > 0 || site.Domain.IndexOf('\n') > 0) ? "多例均衡" : site.Domain, new UIClick(new WebMeta(request.Arguments).Put(g, "Domain")).Send(request.Model, request.Command))

                .AddCell("请求配置", String.IsNullOrEmpty(site.HeaderConf) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "HeaderConf")).Send(request.Model, request.Command))
                .NewSection() .AddCell("时效参数", String.Format("{0}s:{1}m", site.Timeout ?? 100, site.AuthExpire ?? 30), new UIClick(new WebMeta(request.Arguments).Put(g, "Timeout")).Send(request.Model, request.Command))

                .AddCell("应用管理员", String.IsNullOrEmpty(site.AdminConf) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "AdminConf")).Send(request.Model, request.Command));






                var ui2 = ui.NewSection().AddCell("应用目录", "配置", new UIClick(new WebMeta(request.Arguments).Put(g, "Path")).Send(request.Model, request.Command));


                var path = new Hashtable();

                if (String.IsNullOrEmpty(site.Conf) == false)
                {
                    var v = UMC.Data.JSON.Deserialize(site.Conf) as Hashtable;
                    if (v != null)
                    {
                        path = v;
                    }
                }
                var pem = path.GetEnumerator();
                while (pem.MoveNext())
                {
                    var pcell = UICell.Create("UI", new WebMeta().Put("value", pem.Value.ToString(), "text", pem.Key.ToString()));
                    ui2.Delete(pcell, new UIEventText("移除").Click(new UIClick(new WebMeta(request.Arguments).Put(g, pem.Key.ToString())).Send(request.Model, request.Command)));
                }

                var hosts = DataFactory.Instance().Host(site.Root);
                ui2 = ui.NewSection().AddCell("绑定域名", "配置", new UIClick(new WebMeta(request.Arguments).Put(g, "Host")).Send(request.Model, request.Command));
                foreach (var h in hosts)
                {
                    var pcell = UICell.Create("UI", new WebMeta().Put("text", h.Host));
                    ui2.Delete(pcell, new UIEventText("移除").Click(new UIClick(new WebMeta(request.Arguments).Put(g, h.Host)).Send(request.Model, request.Command)));


                }

                if (hosts.Length > 0)
                {
                    var hm = "登录页选择";
                    switch ((site.HostModel ?? HostModel.Select))
                    {
                        case HostModel.Login:
                            hm = "登录页跳转";
                            break;
                        case HostModel.Check:
                            hm = "浏览器中跳转";
                            break;
                        case HostModel.Disable:
                            hm = "全域名跳转";
                            break;
                    }
                    ui2.AddCell("域名反代", hm, new UIClick(new WebMeta(request.Arguments).Put(g, "HostModel")).Send(request.Model, request.Command));


                }

                ui.NewSection()
                        .AddCell("动静分离", String.IsNullOrEmpty(site.StaticConf) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "StaticConf")).Send(request.Model, request.Command))
                        //.AddCell("日志埋点", String.IsNullOrEmpty(site.LogPathConf) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "LogPathConf")).Send(request.Model, request.Command))
                        .AddCell("许可路径", String.IsNullOrEmpty(site.AuthConf) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "AuthConf")).Send(request.Model, request.Command));




                ui2 = ui.NewSection().AddCell("追加脚本", "配置", new UIClick(new WebMeta(request.Arguments).Put(g, "AppendJSConf")).Send(request.Model, request.Command));




                var jsPaths = SiteConfig.Config(site.AppendJSConf);
                foreach (var key in jsPaths)
                {
                    ui2.AddCell(key, new UIClick(String.Format("SITE_JS_CONFIG_{0}{1}", site.Root, MD5(key as string)).ToUpper()).Send(request.Model, "Conf"));
                }


                ui2 = ui.NewSection().AddCell("内容转化", String.IsNullOrEmpty(site.HostReConf) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "HostReConf")).Send(request.Model, request.Command));


                ui2.AddCell("透传会话", String.IsNullOrEmpty(site.OutputCookies) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "OutputCookie")).Send(request.Model, request.Command));

                var m = "标准模式";
                switch ((site.UserModel ?? UserModel.Standard))
                {
                    case UserModel.Bridge:
                        m = "桥接模式";
                        break;
                    case UserModel.Share:
                        m = "共享模式";
                        break;
                    case UserModel.Quote:
                        m = "引用模式";
                        break;
                    case UserModel.Check:
                        m = "自主检测";
                        break;
                    case UserModel.Checked:
                        m = "自动检测";
                        break;
                }
                ui.NewSection().AddCell("异常机器人", String.IsNullOrEmpty(site.Webhook) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "Webhook")).Send(request.Model, request.Command));

                ui.NewSection().AddCell("账户对接模式", m, new UIClick(new WebMeta(request.Arguments).Put(g, "UserModel")).Send(request.Model, request.Command))

                .AddCell("触发登录页面", String.IsNullOrEmpty(site.LogoutPath) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "LogoutPath")).Send(request.Model, request.Command));


                ui.NewSection().AddCell("账户登录接口", new UIClick(String.Format("{0}_Login", site.Root)).Send(request.Model, "Mime"));

                ui.NewSection().AddCell("密码托管接口", new UIClick(String.Format("{0}_Update", site.Root)).Send(request.Model, "Mime"));

                ui.NewSection().AddCell("账户检测接口", new UIClick(String.Format("{0}_Check", site.Root)).Send(request.Model, "Mime"));


                ui.UIFootBar = new UIFootBar() { IsFixed = true };
                ui.UIFootBar.AddText(new UIEventText("可见授权").Click(new UIClick(new WebMeta(request.Arguments).Put(g, "Auth")).Send(request.Model, request.Command)),
                 new UIEventText("重新加载").Click(new UIClick(new WebMeta(request.Arguments).Put(g, "Reload")).Send(request.Model, request.Command)).Style(new UIStyle().BgColor()));


                response.Redirect(ui);
                return this.DialogValue("none");
            });

            if (request.IsMaster == false)
            {
                var userName = UMC.Data.Utility.GetUsername();
                if (SiteConfig.Config(site.AdminConf).Any(r => r == userName) == false)
                {
                    this.Prompt("配置应用的需要应用管理员权限");
                }

            }
            switch (Model)
            {
                case "HostModel":
                    SetHostModel(site);
                    break;
                case "Copy":
                    if (request.IsMaster == false)
                    {
                        this.Prompt("复制应用需要管理员权限");
                    }
                    this.Copy(site);
                    break;
                case "Quote":
                    this.Quote(site);
                    break;
                case "Share":
                    this.Share(site);
                    break;
                case "UserModel":
                    UseModel(site);
                    break;
                case "Auth":
                    response.Redirect("Settings", "AuthKey", site.Root);
                    break;
                case "Home":
                    this.Home(site);
                    break;
                case "Timeout":
                    this.TimeOut(site);
                    break;
                case "AppendJSConf":
                    this.AppendJSConf(site);
                    break;
                case "HostReConf":
                    this.HostReConf(site);
                    break;
                case "LogoutPath":
                    this.LogoutPath(site);
                    break;
                case "Setting":
                    this.Setting(site);

                    break;

                case "LogPathConf":
                    this.LogPathConf(site);
                    break;
                case "AdminConf":
                    this.AdminConf(site);
                    break;
                case "Account":
                    this.Account(site);
                    break;
                case "OutputCookie":
                    this.OutputCookie(site);
                    break;
                case "Path":
                    this.Path(site);
                    break;
                case "Host":
                    this.Host(site);
                    break;
                case "Webhook":
                    Webhook(site);
                    break;
                case "Domain":
                    this.Domain(site);
                    break;
                case "Delete":
                    if (request.IsMaster == false)
                    {
                        this.Prompt("移除应用需要管理员权限");
                    }
                    this.Delete(site);
                    break;
                case "Reload":
                    DataFactory.Instance().Delete(new SiteConfig { Root = site.Root });
                    this.Prompt("重新加载已经就位");
                    break;
                case "HeaderConf":
                    this.HeaderConf(site);
                    break;
                case "AuthConf":
                    this.AuthConf(site);
                    break;
                case "StaticConf":
                    this.StaticConf(site);
                    break;
                default:
                    if (Model.StartsWith("/"))
                    {
                        var path = new Hashtable();

                        if (String.IsNullOrEmpty(site.Conf) == false)
                        {
                            var v = UMC.Data.JSON.Deserialize(site.Conf) as Hashtable;
                            if (v != null)
                            {
                                path = v;
                            }
                        }
                        path.Remove(Model);

                        DataFactory.Instance().Put(new Site { Root = site.Root, Conf = UMC.Data.JSON.Serialize(path) });
                    }
                    else
                    {


                        DataFactory.Instance().Delete(new HostSite { Host = Model });
                    }

                    break;
            }



        }

        private void Host(Site site)
        {
            var host = UIDialog.AsyncDialog("Setting", g =>
           {
               var from = new Web.UIFormDialog() { Title = "绑定域名" };
               from.AddText("域名", "Setting", String.Empty);
               //from.AddText("应用", "Value", String.Empty);
               from.Submit("确认", this.Context.Request, "Site.Config");
               return from;
           });
            if (System.Text.RegularExpressions.Regex.IsMatch(host, @"^([a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?\.)+[a-z0-9]{1,6}$") == false)
            {
                this.Prompt("域名格式不正确");
            }
            DataFactory.Instance().Put(new HostSite
            {
                Root = site.Root,
                Host = host
            });

            this.Context.Send("Site.Config", true);
        }

        private void Domain(Site site)
        {
            var Setting = this.AsyncDialog("Setting", g =>
            {

                var from2 = new UIFormDialog() { Title = "负载网址" };

                from2.AddText("负载域名", "Host", site.Host).PlaceHolder("默认取反代的域名").NotRequired();
                from2.AddRadio("均衡策略", "SLB")
                .Put("随机", "0", (site.SLB ?? 0) == 0)
                .Put("IP", "1", site.SLB == 1)
                .Put("Cookie", "2", site.SLB == 2);

                from2.AddTextarea("网址", "Domain", site.Domain).Put("Rows", 10);//.PlaceHolder("服务网址");

                from2.AddPrompt("后缀[0-9]表示负载均衡权重参数，后缀@user表示用户灰度");

                from2.Submit("确认", this.Context.Request, "Site.Config");
                return from2;
            });


            var Domain = Setting["Domain"];
            var doms = Domain.Split(',', '\n');
            foreach (var v in doms)
            {
                if (v.StartsWith("http://") == false && v.StartsWith("https://") == false)
                {
                    this.Prompt("格式不正确，请输入正确的网址");
                }
            }
            var host = Setting["Host"] ?? String.Empty;
            if (String.IsNullOrEmpty(host) == false)
            {
                switch (host)
                {
                    case "*":
                        break;
                    case "none":
                        host = String.Empty;
                        break;
                    default:
                        if (System.Text.RegularExpressions.Regex.IsMatch(host, @"^([a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?\.)+[a-z0-9]{1,6}$") == false)
                        {
                            this.Prompt("负载域名格式不正确");
                        }

                        break;
                }
            }
            DataFactory.Instance().Put(new Site
            {
                Root = site.Root,
                Domain = Domain,
                Host = host,
                SLB = UMC.Data.Utility.IntParse(Setting["SLB"], 0)
            }); ;
            this.Context.Send("Site.Config", true);
        }
        private void LogPathConf(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "日志埋点" };
                from.AddTextarea("埋点路径", "LogPathConf", site.LogPathConf).PlaceHolder("路径配置格式").Put("Rows", 10);

                from.AddPrompt("埋点路径的日志强制收录，用换行、空格或逗号分割多项");
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var Key = config["LogPathConf"];

            DataFactory.Instance().Put(new Site { Root = site.Root, LogPathConf = Key });

            this.Context.Send("Site.Config", true);
        }
        private void AuthConf(Site site)
        {
            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "许可路径" };
                from.AddTextarea("许可路径", "AuthConf", site.AuthConf).PlaceHolder("路径配置格式").Put("Rows", 6);

                from.AddPrompt("多项用换行、空格或逗号分割，支持“*”前后取配");
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var Key = config["AuthConf"] ?? String.Empty;
            if (String.Equals("none", Key))
            {
                Key = String.Empty;
            }

            DataFactory.Instance().Put(new Site { Root = site.Root, AuthConf = Key });

            this.Context.Send("Site.Config", true);
        }
        private void AppendJSConf(Site site)
        {

            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "追加脚本的路径" };

                from.AddTextarea("页面路径", "AppendJSConf", site.AppendJSConf).PlaceHolder("路径配置格式").Put("Rows", 6);

                from.AddPrompt("多项用换行、空格或逗号分割");
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var Key = config["AppendJSConf"];

            DataFactory.Instance().Put(new Site { Root = site.Root, AppendJSConf = Key });

            this.Context.Send("Site.Config", true);
        }

        private void StaticConf(Site site)
        {

            var config = this.AsyncDialog("Config", g =>
            {
                var from = new Web.UIFormDialog() { Title = "动静分离" };
                from.AddTextarea("不分离路径", "StaticConf", site.StaticConf).Put("Rows", 10).PlaceHolder("配置不分离的路径");

                from.AddPrompt("默认对文件名为gif、ico、svg、bmp、png、jpg、jpeg、css、less、sass、scss、js、webp、jsx、coffee、ts、ttf、woff、woff2、wasm进行静态分离，分离参数all、user、 one、[num]");
                from.Submit("确认", this.Context.Request, "Site.Config");
                return from;
            });
            var Key = config["StaticConf"];


            DataFactory.Instance().Put(new Site { Root = site.Root, StaticConf = Key });

            this.Context.Send("Site.Config", true);
        }

    }
}