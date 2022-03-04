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

namespace UMC.Proxy.Activities
{
    [UMC.Web.Mapping("Proxy", "App", Auth = WebAuthType.User)]
    class SiteAppActivity : UMC.Web.WebActivity
    {
        public override void ProcessActivity(WebRequest request, WebResponse response)
        {
            var home = DataFactory.Instance().WebDomain();
            var union = Data.WebResource.Instance().Provider["union"] ?? ".";
            var Key = this.AsyncDialog("Key", g =>
            {
                var auth = String.Empty;
                var type = this.AsyncDialog("Type", gt =>
                {
                    if (request.UserAgent.Contains("DingTalk"))
                    {
                        if (request.UserAgent.Contains("Windows NT") || request.UserAgent.Contains("Mac OS X"))
                        {
                            var seesionKey = Utility.MD5(this.Context.Token.Id.Value);

                            var sesion = UMC.Data.DataFactory.Instance().Session(this.Context.Token.Id.ToString());

                            if (sesion != null)
                            {
                                sesion.SessionKey = seesionKey;

                                UMC.Data.DataFactory.Instance().Post(sesion);
                            }
                            return this.DialogValue("Auth");
                        }
                    }

                    return this.DialogValue("ALL");

                });
                switch (type)
                {
                    case "Auth":
                        auth = $"/!/{Utility.MD5(this.Context.Token.Id.Value)}";
                        break;
                }

                var sts = new System.Data.DataTable();
                sts.Columns.Add("title");
                sts.Columns.Add("root");
                sts.Columns.Add("url");
                sts.Columns.Add("src");
                sts.Columns.Add("target");
                sts.Columns.Add("badge");
                sts.Columns.Add("desktop", typeof(bool));
                sts.Columns.Add("docs");


                if (request.IsMaster)
                {
                    sts.Rows.Add("应用设置", "UMC", "/Setting/",
                      "/css/images/icon/prefapp.png", "max", "", true, new Uri(request.Url, "/Docs/").AbsoluteUri);
                    sts.Rows.Add("帮助文档", "UMC", "/Docs/",
                   "/css/images/icon/ibooks.png", "max", "", true, new Uri(request.Url, "/Docs/").AbsoluteUri);

                }

                var keys = new List<String>();
                var user = this.Context.Token.Identity();// UMC.Security.Identity.Current;


                UMC.Data.Session<UMC.Web.WebMeta> session = new Data.Session<WebMeta>(user.Id.ToString() + "_Desktop");

                var desktop = session.Value ?? new WebMeta();
                var sites = DataFactory.Instance().Site().Where(r => r.Flag != -1).Where(r => (r.IsModule ?? false) == false)
                .OrderBy((arg) => arg.Caption).ToList();

                sites.Any(r => { keys.Add(r.Root); return false; });
                var auths = UMC.Security.AuthManager.IsAuthorization(user, keys.ToArray());

                var webr = UMC.Data.WebResource.Instance();
                var ds = sites.ToArray();
                for (var i = 0; i < ds.Length; i++)
                {
                    var d = ds[i];

                    if (auths[i] || SiteConfig.Config(d.AdminConf).Contains(user.Name))
                    {
                        var domain = $"{request.Url.Scheme}://{d.Root}{union}{home}{auth}/?login";

                        var docs = $"{request.Url.Scheme}://{d.Root}{union}{home}/Docs/";

                        var title = d.Caption ?? ""; ;
                        var vindex2 = title.IndexOf("v.", StringComparison.CurrentCultureIgnoreCase);
                        if (vindex2 > -1)
                        {
                            title = title.Substring(0, vindex2);
                        }
                        var badge = "";

                        var target = "_blank";
                        switch (d.OpenModel ?? 0)
                        {
                            case 1:
                                target = "normal";
                                break;
                            case 2:
                                target = "max";
                                break;
                        }
                        if ((d.OpenModel ?? 0) == 3)
                        {
                            domain = new Uri(new Uri(SiteConfig.Config(d.Domain)[0]), d.Home ?? "/").AbsoluteUri;
                        }
                        else if (SiteConfig.Config(d.AuthConf).Contains("*") || d.AuthType == WebAuthType.All)
                        {
                            domain = $"{request.Url.Scheme}://{d.Root}{union}{home}{d.Home}";
                        }
                        var isDesktop = desktop.ContainsKey(d.Root);
                        if (d.IsDesktop == true)
                        {
                            if (String.Equals("hide", desktop[d.Root]))
                            {
                                isDesktop = false;
                            }
                            else
                            {
                                isDesktop = true;
                            }

                        }


                        sts.Rows.Add(title.Trim(), d.Root, domain, webr.ImageResolve(Data.Utility.Guid(d.Root, true).Value, "1", 4), target, badge, isDesktop, docs);
                    }
                }
                response.Redirect(sts);


                return this.DialogValue("none");
            });
            var site = DataFactory.Instance().Site(Key);
            if (site == null)
            {
                this.Prompt("标准组件，不支持此操作");
            }
            var caption = site.Caption;
            var vindex = caption.IndexOf("v.", StringComparison.CurrentCultureIgnoreCase);

            var version = (site.Version ?? "01");
            if (vindex > -1)
            {
                caption = caption.Substring(0, vindex);
                //version = caption.Substring(vindex);
            }
            var Model = this.AsyncDialog("Model", gkey =>
            {
                WebMeta form = request.SendValues ?? new UMC.Web.WebMeta();

                if (form.ContainsKey("limit") == false)
                {
                    this.Context.Send(new UISectionBuilder(request.Model, request.Command, request.Arguments)
                            .Builder(), true);

                }


                var ui = UMC.Web.UISection.Create(new UITitle("关于应用"));


                var Discount = new UIHeader.Portrait(Data.WebResource.Instance().ImageResolve(Data.Utility.Guid(site.Root, true).Value, "1", 4));


                Discount.Value(caption);


                var color = 0x28CA40;
                Discount.Gradient(color, color);

                var header = new UIHeader();

                var style = new UIStyle();
                header.AddPortrait(Discount);
                header.Put("style", style);



                ui.UIHeader = header;
                ui.AddCell("版本", version);

                switch (site.UserModel ?? UserModel.Standard)
                {
                    default:
                    case UserModel.Standard:
                        ui.AddCell("账户对接", "标准模式");
                        break;
                    case UserModel.Share:
                        ui.AddCell("账户对接", "共享模式");
                        break;
                    case UserModel.Quote:
                        ui.AddCell("账户对接", "引用模式");
                        break;
                    case UserModel.Bridge:
                        ui.AddCell("账户对接", "桥接模式");
                        break;
                }

                var ui3 = ui.NewSection();
                ui3.Header.Put("text", "应用管理员");
                var ads = SiteConfig.Config(site.AdminConf);
                var user = this.Context.Token.Identity(); // UMC.Security.Identity.Current;


                if (ads.Length > 0)
                {
                    foreach (var v in ads)
                    {
                        if (String.Equals(user.Name, v))
                        {
                            ui3.AddCell('\uf2c0', v, "应用管理", new UIClick(site.Root).Send(request.Model, "Site"));
                        }
                        else
                        {

                            ui3.AddCell('\uf2c0', v, "");
                        }
                    }
                }
                else
                {
                    ui3.Add("Desc", new UMC.Web.WebMeta().Put("desc", "未设置应用管理员").Put("icon", "\uEA05"), new UMC.Web.WebMeta().Put("desc", "{icon}\n{desc}"),
                  new UIStyle().Align(1).Color(0xaaa).Padding(20, 20).BgColor(0xfff).Size(12).Name("icon", new UIStyle().Font("wdk").Size(60)));//.Name 

                }


                response.Redirect(ui);
                return this.DialogValue("none");
            });
            switch (Model)
            {
                case "PlusDesktop":
                    {
                        var user = this.Context.Token.Identity();// UMC.Security.Identity.Current;
                        UMC.Data.Session<UMC.Web.WebMeta> session = new Data.Session<WebMeta>(user.Id.ToString() + "_Desktop");
                        var value = session.Value ?? new WebMeta();
                        value.Put(site.Root, true);

                        session.ContentType = "Settings";
                        session.Commit(value, user.Id.Value, true, request.UserHostAddress);
                        response.Redirect(new WebMeta().Put("Desktop", true));
                    }
                    break;
                case "RemoveDesktop":
                    {
                        var user = this.Context.Token.Identity(); //UMC.Security.Identity.Current;
                        UMC.Data.Session<UMC.Web.WebMeta> session = new Data.Session<WebMeta>(user.Id.ToString() + "_Desktop");
                        var value = session.Value ?? new WebMeta();
                        if (site.IsDesktop == true)
                        {
                            value.Put(site.Root, "hide");
                        }
                        else
                        {
                            value.Remove(site.Root);

                        }
                        session.ContentType = "Settings";
                        session.Commit(value, user.Id.Value, true, request.UserHostAddress);
                        response.Redirect(new WebMeta().Put("Desktop", true));
                    }

                    break;
                case "Account":
                    {
                        var user = this.Context.Token.Identity(); //UMC.Security.Identity.Current;
                        switch (site.UserModel ?? UserModel.Standard)
                        {
                            case UserModel.Check:
                            default:
                            case UserModel.Checked:
                            case UserModel.Standard:
                                break;
                            case UserModel.Bridge:
                                this.Prompt("此应用不支持设置多账户");
                                break;
                        }

                        var scookies = DataFactory.Instance().Cookies(site.Root, user.Id.Value).OrderBy(r => r.IndexValue).ToList();
                        var login = UMC.Data.Utility.TimeSpan();

                        var vt = login;
                        foreach (var sc in scookies)
                        {
                            if (String.IsNullOrEmpty(sc.Account))
                            {
                                login = sc.IndexValue ?? 0;
                                break;
                            }
                        }
                        if (login <= 0)
                        {
                            this.Prompt("请先设置自己主账户");
                        }
                        else
                        {
                            if (vt == login)
                            {
                                DataFactory.Instance().Put(new Entities.Cookie() { IndexValue = login, user_id = user.Id, Domain = site.Root });
                            }

                            this.Context.Send("Desktop.Open", new WebMeta("title", caption, "id", site.Root, "text", "多账户对接")
                                .Put("src", String.Format("{0}://{1}{2}{3}/?$=New", request.Url.Scheme, site.Root, union, home
                                 , login)).Put("max", true), true);
                        }
                    }
                    break;
                case "Delete":
                    {
                        var ls = DataFactory.Instance().Cookies(site.Root, this.Context.Token.UId.Value)
                            .Where(r => String.IsNullOrEmpty(r.Account) == false).ToArray();
                        if (ls.Length == 0)
                        {
                            this.Prompt("还未绑定账户，不需要移除");
                        }
                        var indexValue = UMC.Data.Utility.IntParse(this.AsyncDialog("IndexValue", k =>
                        {
                            if (ls.Length == 1)
                            {
                                return new UIConfirmDialog("您确认移除此应用的绑定吗") { DefaultValue = (ls[0].IndexValue ?? 0).ToString() };
                            }
                            else
                            {
                                var dc = new UISheetDialog() { Title = "请选择移除账户" };
                                foreach (var c in ls)
                                {
                                    if (String.IsNullOrEmpty(c.Account) == false && c.IndexValue != 0)
                                    {
                                        dc.Options.Add(new UIClick(new WebMeta(request.Arguments).Put(k, c.IndexValue)) { Text = c.Account }.Send(request.Model, request.Command));
                                    }
                                }
                                return dc;
                            }
                        }), 0);
                        //var user = this.Context.Token.Identity(); // UMC.Security.Identity.Current;//.Id.Value
                        UMC.Data.DataFactory.Instance().Delete(new Password { Key = SiteConfig.MD5Key(site.Root, this.Context.Token.UId.Value, indexValue) });
                        DataFactory.Instance().Delete(new Cookie { user_id = this.Context.Token.UId.Value, Domain = site.Root, IndexValue = indexValue });

                        this.Prompt(String.Format("解除账户绑定成功", site.Caption));
                    }
                    break;
                case "Password":
                    {
                        var ls = DataFactory.Instance().Cookies(site.Root, this.Context.Token.UId.Value)
                               .Where(r => String.IsNullOrEmpty(r.Account) == false).ToArray();
                        if (ls.Length == 0)
                        {
                            this.Prompt("您未对接此应用");
                        }

                        var indexValue = UMC.Data.Utility.IntParse(this.AsyncDialog("IndexValue", k =>
                        {
                            var dc = new UISheetDialog() { Title = "请选择账户" };
                            foreach (var c in ls)
                            {
                                if (String.IsNullOrEmpty(c.Account) == false)
                                {
                                    dc.Options.Add(new UIClick(new WebMeta(request.Arguments).Put(k, c.IndexValue)) { Text = c.Account }.Send(request.Model, request.Command));
                                }
                            }
                            if (dc.Options.Count < 2)
                            {
                                return this.DialogValue(ls[0].IndexValue.ToString());
                            }
                            return dc;
                        }), 0);
                        var cookie = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(site.Root, this.Context.Token.UId.Value, indexValue));
                        if (String.IsNullOrEmpty(cookie) == false)
                        {
                            this.Context.Send("Clipboard", new WebMeta().Put("text", cookie), true);
                        }
                        else
                        {
                            this.Prompt("您未对接此应用");
                        }
                    }
                    break;
            }
        }

    }
}