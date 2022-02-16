using System;
using System.Collections.Generic;
using System.Text;

namespace UMC.Proxy.Entities
{
    public enum UserModel
    {
        Standard = 0,
        /// <summary>
        /// 共享
        /// </summary>
        Share = 1,
        /// <summary>
        /// 引用
        /// </summary>
        Quote = 3,
        /// <summary>
        /// 桥接
        /// </summary>
        Bridge = 4,
        /// <summary>
        /// 管理员检测密码
        /// </summary>
        Check = 5,
        /// <summary>
        /// 管理员检测密码
        /// </summary>
        Checked = 6


    }
    public enum HostModel
    {
        /// <summary>
        /// 在登录入口选择
        /// </summary>
        Select = 0,
        /// <summary>
        /// 在登录入口跳转
        /// </summary>
        Login = 1,
        /// <summary>
        /// 游览器跳转
        /// </summary>
        Check = 3,
        /// <summary>
        /// 所有请求跳转
        /// </summary>
        Disable = 4

    }
    public enum UserBrowser
    {
        All = 0,
        Chrome = 1,
        Firefox = 2,
        IE = 4,
        WebKit = 8,
        //Opera = 16,
        Dingtalk = 32,
        WeiXin = 64

    }

    public class Site
    {
        public string Root
        {
            get;
            set;
        }
        public string Host
        {
            get;
            set;
        }

        public int? SiteKey
        {
            get; set;
        }
        public int? Timeout { get; set; }
        public string Caption { get; set; }

        /// <summary>
        /// 应用子目录
        /// </summary>
        public String Conf { get; set; }

        public String AuthConf { get; set; }
        public String StaticConf { get; set; }
        public String AppendJSConf { get; set; }
        public string Domain { get; set; }
        public int? Type { get; set; }
        /// <summary>
        /// 服务账户
        /// </summary>
        public string Account
        {
            get; set;
        }
        /// <summary>
        /// 客户端版本
        /// </summary>
        public string Version
        {
            get; set;
        }

        public string HelpKey
        {
            get; set;
        }
        public string OutputCookies
        {
            get; set;
        }
        public DateTime? Time
        {
            get; set;
        }
        /// <summary>
        /// 登录后的主页
        /// </summary>
        public string Home { get; set; }
        /// <summary>
        /// 移动主页
        /// </summary>
        //public string MobileHome
        //{
        //    get; set;
        //}
        public int? OpenModel { get; set; }

        public UserModel? UserModel { get; set; }


        public Web.WebAuthType? AuthType { get; set; }

        public int? AuthExpire { get; set; }

        /// <summary>
        /// 标签，-1逻辑删除
        /// </summary>
        public int? Flag { get; set; }


        public int? SLB { get; set; }

        /// <summary>
        /// 请求头配置
        /// </summary>
        public string HeaderConf
        {
            get; set;
        }
        /// <summary>
        /// 日志地址
        /// </summary>
        public string LogPathConf
        {
            get; set;
        }
        /// <summary>
        /// 退出地址
        /// </summary>
        public string LogoutPath
        {
            get; set;
        }
        /// <summary>
        /// 替换域名的路径
        /// </summary>
        public string HostReConf
        {
            get; set;
        }

        /// <summary>
        /// 配置管理人
        /// </summary>
        public string AdminConf
        {
            get; set;
        }
        /// <summary>
        /// 是否是模块
        /// </summary>
        public bool? IsModule
        {
            get; set;
        }
        /// <summary>
        /// 是否是显示在桌面
        /// </summary>
        public bool? IsDesktop
        {
            get; set;
        }
        /// <summary>
        /// 是否是调试模式
        /// </summary>
        public bool? IsDebug
        {
            get; set;
        }

        ///// <summary>
        ///// 支持的浏览器
        ///// </summary>
        public UserBrowser? UserBrowser
        {
            get; set;
        }


        public HostModel? HostModel
        {
            get; set;
        }
        /// <summary>
        /// 机器人地址
        /// </summary>
        public string Webhook { get; set; }
        /// <summary>
        /// 机器人签名
        /// </summary>
        public string WebhookSecret { get; set; }


    }
    public class HostSite
    {
        public string Host
        {
            get; set;
        }
        public string Root
        {
            get; set;
        }
    }

}
