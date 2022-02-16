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
    /// <summary>
    /// 邮箱账户
    /// </summary>
    [UMC.Web.Mapping("Proxy", "Conf", Auth = WebAuthType.User)]
    class SiteConfActivity : WebActivity
    {
        public override void ProcessActivity(WebRequest request, WebResponse response)
        {
            var mainKey = this.AsyncDialog("Key", g =>
            {
                this.Prompt("请传入KEY");
                return this.DialogValue("none");
            }); 
            var config = UMC.Data.DataFactory.Instance().Config(mainKey);
            var ConfValue = UIDialog.AsyncDialog("ConfValue", g =>
            {
                var title = "内容配置";
                if (mainKey.StartsWith("SITE_JS_CONFIG_"))
                {
                    title = "脚本配置";
                } 
                var from5 = new UIFormDialog() { Title = title };
                from5.AddTextarea(title, "ConfValue", config != null ? config.ConfValue : "").Put("Rows", 20);

                from5.Submit("确认", this.Context.Request, "Mime.Config");
                return from5;
                 
            });
            if (mainKey.StartsWith("SITE_")==false)
            {
                this.Prompt("只能配置站点相关内容");
            }

            Config platformConfig = new Config();
            platformConfig.ConfKey = mainKey;
            platformConfig.ConfValue = ConfValue;
            UMC.Data.DataFactory.Instance().Put(platformConfig);
            this.Context.Send("Mime.Config", true);
        }
    }
}