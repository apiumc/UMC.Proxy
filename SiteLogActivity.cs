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
using System.Text;

namespace UMC.Proxy.Activities
{
    /// <summary>
    /// 邮箱账户
    /// </summary>
    [UMC.Web.Mapping("Proxy", "Log", Auth = WebAuthType.User)]
    class SiteLogActivity : UMC.Web.WebActivity
    {
        public static string GetSpValue(int sp)
        {
            var sb = new StringBuilder();
            var t = TimeSpan.FromMilliseconds(sp);
            if (t.Days > 0)
            {
                sb.Append(t.Days);
                sb.Append("天");
            }
            if (t.Hours > 0)
            {
                sb.Append(t.Hours);
                sb.Append("小时");

            }
            if (t.Minutes > 0)
            {
                sb.Append(t.Minutes);
                sb.Append("分");

            }
            if (t.Seconds > 0)
            {
                sb.Append(t.Seconds);
                sb.Append("秒");
            }
            if (sb.Length == 0)
            {
                sb.Append(t.Milliseconds > 0 ? "不到1秒" : "0秒");
            }
            return sb.ToString();
        }
        public static string GetDate(int sp, string type)
        {
            var date = UMC.Data.Utility.TimeSpan(sp);
            switch (type)
            {
                default:
                case "day":
                    return date.Hour + "点钟";
                case "week":
                    switch (date.DayOfWeek)
                    {
                        default:
                        case DayOfWeek.Monday:
                            return "周一";
                        case DayOfWeek.Tuesday:
                            return "周二";
                        case DayOfWeek.Wednesday:
                            return "周三";
                        case DayOfWeek.Thursday:
                            return "周四";
                        case DayOfWeek.Friday:
                            return "周五";
                        case DayOfWeek.Saturday:
                            return "周六";
                        case DayOfWeek.Sunday:
                            return "周日";
                    }
                case "month":
                    return date.Day + "日";
            }
        }
        public override void ProcessActivity(WebRequest request, WebResponse response)
        {
            var status = UMC.Data.Utility.IntParse(this.AsyncDialog("status", "200"), 200);
            var user = this.Context.Token.Identity(); //UMC.Security.Identity.Current;
            var siteKey = this.AsyncDialog("site", "SELF");

            var date = Convert.ToDateTime(this.AsyncDialog("date", d =>
            {
                return this.DialogValue(DateTime.Now.AddDays(-1).ToShortDateString());
            }));
            var type = this.AsyncDialog("type", "day");
            int intType = 0;
            var time = 60 * 60;
            switch (type)
            {
                default:
                case "day":
                    time = 60 * 60;
                    break;
                case "week":
                    intType = 1;
                    time = 60 * 60 * 24;
                    break;
                case "month":
                    intType = 2;
                    time = 60 * 60 * 24;
                    break;
            }



            var total = new Log() { Duration = 0, Quantity = 0, Time = 0 };
            var data = new System.Data.DataTable();
            data.Columns.Add("time");
            data.Columns.Add("date");
            data.Columns.Add("quantity");
            data.Columns.Add("users");
            data.Columns.Add("duration");
            data.Columns.Add("sites", typeof(System.Data.DataTable));
            var search = new Log { Time = UMC.Data.Utility.TimeSpan(date) };
            var isLog = false;
            if (String.Equals(siteKey, "SELF"))
            {
                search.Username = user.Name;
            }
            else
            {

                search.Key = siteKey;
                var uName = this.AsyncDialog("user", "none");
                switch (uName)
                {
                    case "SELF":
                        isLog = true;
                        search.Username = user.Name;
                        search.Quantity = UMC.Data.Utility.IntParse(this.AsyncDialog("Quantity", "3"), 3);
                        break;
                    case "none":
                        break;
                    default:
                        isLog = true;
                        search.Username = uName;// user.Name;

                        search.Quantity = UMC.Data.Utility.IntParse(this.AsyncDialog("Quantity", "3"), 3);
                        break;
                }
            }


            if (status >= 400)
            {
                search.Time = 0 - search.Time.Value;
            }
            var sites = DataFactory.Instance().Site();
            var logs = DataFactory.Instance().Search(search, intType);
            if (isLog)
            {
                String caption = null;
                var site = sites.FirstOrDefault(r => r.Root == siteKey);
                if (site != null)
                {
                    caption = site.Caption;
                    var vindex = caption.IndexOf("v.", StringComparison.CurrentCultureIgnoreCase);
                    if (vindex > -1)
                    {
                        caption = caption.Substring(0, vindex);
                    }

                    response.Redirect(new WebMeta().Put("data", logs).Put("title", caption).Put("user", search.Username));
                }

            }

            var counts = new List<String>();
            var usersTotal = 0;
            if (logs.Length > 0)
            {
                var home = DataFactory.Instance().WebDomain();
                var log = new Log() { Duration = 0, Quantity = 0, Time = logs[0].Time };
                var timeUserTotal = 0;
                var detail = new System.Data.DataTable();
                detail.Columns.Add("key");
                detail.Columns.Add("caption");
                detail.Columns.Add("quantity");
                detail.Columns.Add("duration");
                for (var i = 0; i < logs.Length; i++)
                {
                    var l = logs[i];
                    total.Quantity += l.Quantity ?? 1;
                    total.Duration += l.Duration ?? 0;
                    if (l.Time == log.Time)
                    {
                        log.Quantity += l.Quantity ?? 1;
                        log.Duration += l.Duration ?? 0;
                    }
                    else
                    {
                        data.Rows.Add(GetDate(log.Time.Value * time, type), UMC.Data.Utility.TimeSpan(log.Time.Value * time).ToShortDateString(), log.Quantity ?? 1, timeUserTotal, GetSpValue(log.Duration ?? 0), detail);
                        log = new Log() { Duration = l.Duration ?? 0, Quantity = l.Quantity ?? 1, Time = l.Time };
                        timeUserTotal = 0;

                        detail = new System.Data.DataTable();
                        detail.Columns.Add("key");
                        detail.Columns.Add("caption");
                        detail.Columns.Add("quantity");
                        detail.Columns.Add("duration");

                    }
                    if (String.Equals(siteKey, "SELF"))
                    {
                        timeUserTotal = 1;
                        usersTotal = 1;
                    }
                    else
                    {
                        timeUserTotal += UMC.Data.Utility.IntParse(l.Username, 1);

                        usersTotal += timeUserTotal;
                    }
                    if (counts.Exists(r => r == l.Key) == false)
                    {
                        counts.Add(l.Key);
                    }
                    var site = sites.FirstOrDefault(r => r.Root == l.Key);
                    if (site != null)
                    {
                        if (status >= 400)
                        {
                            detail.Rows.Add(l.Key, l.Status ?? 0, l.Quantity ?? 1, GetSpValue(l.Duration ?? 0));
                        }
                        else
                        {
                            var caption = site.Caption;
                            var vindex = caption.IndexOf("v.", StringComparison.CurrentCultureIgnoreCase);
                            if (vindex > -1)
                            {
                                caption = caption.Substring(0, vindex);
                            }
                            detail.Rows.Add(l.Key, caption, l.Quantity ?? 1, GetSpValue(l.Duration ?? 0));
                        }
                    }
                }
                data.Rows.Add(GetDate(log.Time.Value * time, type), UMC.Data.Utility.TimeSpan(log.Time.Value * time).ToShortDateString(), log.Quantity, timeUserTotal, GetSpValue(log.Duration.Value), detail);


            }
            var dateTitle = "";
            switch (type)
            {
                default:
                case "day":
                    var sp = DateTime.Now.Date - date;
                    if (sp.Days == 0)
                    {
                        dateTitle = "今日";
                    }
                    else if (sp.Days == 1)
                    {
                        dateTitle = "昨日";

                    }
                    else
                    {
                        dateTitle = "当日";

                    }
                    break;
                case "week":
                    dateTitle = "当周";
                    break;
                case "month":
                    dateTitle = "当月";
                    break;
            }
            var dc = new WebMeta().Put("total", usersTotal).Put("count", counts.Count).Put("title", dateTitle).Put("date", date.ToString("yyyy-MM-dd")).Put("quantity", total.Quantity ?? 0).Put("duration", GetSpValue(total.Duration ?? 0)).Put("data", data);
            if (String.IsNullOrEmpty(search.Key) == false)
            {
                var d = sites.FirstOrDefault(r => r.Root == search.Key);
                if (d != null)
                {
                    var caption = d.Caption;
                    var vindex = caption.IndexOf("v.", StringComparison.CurrentCultureIgnoreCase);
                    if (vindex > -1)
                    {
                        caption = caption.Substring(0, vindex);
                    }
                    dc.Put("site", caption);
                }
            }
            if (status >= 400)
            {
                dc.Put("error", true);
            }
            response.Redirect(dc);

        }

    }
}