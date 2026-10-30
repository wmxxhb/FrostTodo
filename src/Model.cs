using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FrostTodo {
 public class Todo {
  public string Id {get;set;} public string Title {get;set;} public List<string> Tags {get;set;}
  public string Due {get;set;} public string Time {get;set;} public int Urgency {get;set;} public int Importance {get;set;}
  public string Reminder {get;set;} public bool ReminderFired {get;set;} public string Repeat {get;set;} public int Anchor {get;set;}
  public string RepeatDue {get;set;} public string SnoozedUntil {get;set;} public string SnoozedFor {get;set;}
  public List<int> WeekDays {get;set;} public bool Done {get;set;} public string Created {get;set;}
  public Todo(){Id=Guid.NewGuid().ToString("N");Title="";Tags=new List<string>();Due="";Time="";Reminder="";Repeat="none";RepeatDue="";SnoozedUntil="";SnoozedFor="";WeekDays=new List<int>();Created=DateTime.Now.ToString("o");}
  public Todo Copy(){return new Todo{Id=Id,Title=Title,Tags=new List<string>(Tags),Due=Due,Time=Time,Urgency=Urgency,Importance=Importance,Reminder=Reminder,ReminderFired=ReminderFired,Repeat=Repeat,Anchor=Anchor,RepeatDue=RepeatDue,SnoozedUntil=SnoozedUntil,SnoozedFor=SnoozedFor,WeekDays=new List<int>(WeekDays),Done=Done,Created=Created};}
 }
 public class Preferences {
  public string GptTheme {get;set;}
  public string BackgroundImage {get;set;} public string BackgroundImageName {get;set;}
  public int TextSize {get;set;} public string TextWeight {get;set;} public string TextRendering {get;set;} public int TextContrast {get;set;} public double TextSpacing {get;set;} public int TextLineGap {get;set;}
  public string Tint {get;set;} public string Foreground {get;set;} public string Font {get;set;} public int Opacity {get;set;} public string Density {get;set;} public string Mode {get;set;}
  public string Collapse {get;set;} public string Count {get;set;} public double Width {get;set;} public double Height {get;set;}
  public double Left {get;set;} public double Top {get;set;} public double DotLeft {get;set;} public double DotTop {get;set;}
  public List<string> QuickTags {get;set;} public List<string> QuickViews {get;set;} public bool QuickTagsAll {get;set;} public bool LightBackdropV1 {get;set;}
  public Preferences(){GptTheme="light";TextAppearance.Reset(this);Tint="#F3F6FA";Foreground="auto";Font="system";Opacity=55;Density="balanced";Mode="top";Collapse="dot";Count="all";Width=390;Height=590;Left=-1;Top=-1;DotLeft=-1;DotTop=-1;QuickTags=new List<string>();QuickViews=new List<string>{"urgent","important"};QuickTagsAll=true;UiVariant.Defaults(this);}
 }
 public class AppData {
  public int Version {get;set;} public List<Todo> Tasks {get;set;} public List<string> Themes {get;set;} public Preferences Settings {get;set;}
  public AppData(){Version=1;Tasks=new List<Todo>();Themes=new List<string>{"工作","生活","论文","个人"};Settings=new Preferences();}
  public void Normalize(){
   if(Version>1)throw new InvalidOperationException("备份来自更新版本，请先更新应用。");
   if(Tasks==null)Tasks=new List<Todo>(); if(Themes==null)Themes=new List<string>();if(Settings==null)Settings=new Preferences();
   if(Tasks.Count>20000)throw new InvalidOperationException("任务数量超出支持范围。");
   HashSet<string> ids=new HashSet<string>();
   foreach(Todo t in Tasks){if(t==null||String.IsNullOrWhiteSpace(t.Title))throw new InvalidOperationException("存在空白任务。");if(String.IsNullOrEmpty(t.Id)||!ids.Add(t.Id))throw new InvalidOperationException("任务标识重复或缺失。");if(t.Tags==null)t.Tags=new List<string>();t.Tags=t.Tags.Where(x=>!String.IsNullOrWhiteSpace(x)).Distinct().ToList();if(t.WeekDays==null)t.WeekDays=new List<int>();t.WeekDays=t.WeekDays.Where(n=>n>=0&&n<=6).Distinct().ToList();t.Urgency=Math.Max(0,Math.Min(2,t.Urgency));t.Importance=Math.Max(0,Math.Min(2,t.Importance));if(!Logic.Repeats.Contains(t.Repeat))t.Repeat="none";t.Due=t.Due??"";t.Time=t.Time??"";t.Reminder=t.Reminder??"";
    t.RepeatDue=t.RepeatDue??"";t.SnoozedUntil=t.SnoozedUntil??"";t.SnoozedFor=t.SnoozedFor??"";if(t.RepeatDue!=""&&!Logic.IsDate(t.RepeatDue))throw new InvalidOperationException("重复任务日期格式无效。");if(t.SnoozedUntil!=""&&!Logic.IsStamp(t.SnoozedUntil))throw new InvalidOperationException("稍后提醒格式无效。");
    if(t.Due!=""&&!Logic.IsDate(t.Due))throw new InvalidOperationException("任务截止日期格式无效。");if(t.Time!=""&&!Logic.IsTime(t.Time))throw new InvalidOperationException("任务时间格式无效。");if(t.Reminder!=""&&!Logic.IsStamp(t.Reminder))throw new InvalidOperationException("提醒格式无效。");foreach(string tag in t.Tags)if(!Themes.Contains(tag))Themes.Add(tag);
   }
   Themes=Themes.Where(x=>!String.IsNullOrWhiteSpace(x)).Distinct().ToList();
   Preferences p=Settings; if(p.QuickTags==null)p.QuickTags=new List<string>();if(p.QuickViews==null)p.QuickViews=new List<string>{"urgent","important"};p.QuickViews=p.QuickViews.Where(x=>new[]{"urgent","very-urgent","important","very-important"}.Contains(x)).Distinct().ToList();if(!FontCatalog.Keys.Contains(p.Font))p.Font="system";
   TextAppearance.Normalize(p);if(!new[]{"light","dark","system","custom"}.Contains(p.GptTheme))p.GptTheme="light";
   if(!p.LightBackdropV1){p.Tint="#F3F6FA";p.LightBackdropV1=true;}
   p.Opacity=Math.Max(10,Math.Min(UiVariant.MaximumOpacity,p.Opacity));if(!new[]{"top","normal","bottom"}.Contains(p.Mode))p.Mode="top";
   if(!new[]{"dot","hidden"}.Contains(p.Collapse))p.Collapse="dot";if(!new[]{"compact","balanced","spacious"}.Contains(p.Density))p.Density="balanced";
   if(String.IsNullOrEmpty(p.Count))p.Count="all";if(String.IsNullOrEmpty(p.Tint)||!System.Text.RegularExpressions.Regex.IsMatch(p.Tint,"^#[0-9a-fA-F]{6}$"))p.Tint="#D8E6F2";
   if(String.IsNullOrEmpty(p.Foreground)||(p.Foreground!="auto"&&!System.Text.RegularExpressions.Regex.IsMatch(p.Foreground,"^#[0-9a-fA-F]{6}$")))p.Foreground="auto";
   p.Width=Clamp(p.Width,300,1600,390);p.Height=Clamp(p.Height,330,1800,590);
  }
  static double Clamp(double x,double min,double max,double fallback){return Double.IsNaN(x)||Double.IsInfinity(x)?fallback:Math.Max(min,Math.Min(max,x));}
 }
 public static class Logic {
  public static string NormalizeTime(string value){string s=(value??"").Trim().Replace('：',':');if(s=="")return "";DateTime d;return DateTime.TryParseExact(s,new[]{"H:mm","HH:mm","H:m","HH:m","Hmm","HHmm"},CultureInfo.InvariantCulture,DateTimeStyles.None,out d)?d.ToString("HH:mm"):s;}
  public static bool ThemeVisible(AppData d,string tag){return d.Settings.QuickTagsAll||d.Settings.QuickTags.Contains(tag);}
  public static void SetThemeVisible(AppData d,string tag,bool visible){if(d.Settings.QuickTagsAll){d.Settings.QuickTags=new List<string>(d.Themes);d.Settings.QuickTagsAll=false;}if(visible){if(!d.Settings.QuickTags.Contains(tag))d.Settings.QuickTags.Add(tag);}else d.Settings.QuickTags.Remove(tag);}
  public static bool AddTheme(AppData d,string input){string tag=(input??"").Trim();if(tag==""||tag.Length>16||d.Themes.Contains(tag))return false;d.Themes.Add(tag);if(!d.Settings.QuickTagsAll)d.Settings.QuickTags.Add(tag);return true;}
  public static void RemoveTheme(AppData d,string tag){d.Themes.Remove(tag);d.Settings.QuickTags.Remove(tag);foreach(Todo t in d.Tasks)t.Tags.RemoveAll(x=>x==tag);if(d.Settings.Count=="tag:"+tag)d.Settings.Count="all";}
  public static readonly string[] Repeats={"none","daily","weekdays","weekly","monthly","custom"};
  public static DateTime Date(string s){return DateTime.ParseExact(s,"yyyy-MM-dd",CultureInfo.InvariantCulture);}
  public static bool IsDate(string s){DateTime d;return DateTime.TryParseExact(s,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d);}
  public static bool IsTime(string s){DateTime d;return DateTime.TryParseExact(s,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out d);}
  public static bool IsStamp(string s){DateTime d;return DateTime.TryParseExact(s,"yyyy-MM-dd'T'HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out d);}
  public static DateTime Stamp(string s){return DateTime.ParseExact(s,"yyyy-MM-dd'T'HH:mm",CultureInfo.InvariantCulture);}
  public static string Ymd(DateTime d){return d.ToString("yyyy-MM-dd");}
  public static string DateLabel(string s){if(String.IsNullOrEmpty(s))return "";DateTime d=Date(s);int delta=(d-DateTime.Today).Days;return delta==0?"今天":delta==1?"明天":delta==-1?"昨天":d.ToString(d.Year==DateTime.Today.Year?"M月d日":"yyyy年M月d日");}
  public static string Next(Todo t,DateTime today){
   if(t.Repeat=="none")return t.Due;DateTime? current=Recurrence.Occurrence(t,today);if(!current.HasValue||current.Value==DateTime.MaxValue.Date)return t.Due;DateTime? next=Recurrence.Occurrence(t,current.Value.AddDays(1));return next.HasValue?Ymd(next.Value):t.Due;
  }
  public static void Complete(Todo t,DateTime today){
   if(t.Done){t.Done=false;return;}if(t.Repeat=="none"){t.Done=true;t.SnoozedUntil="";t.SnoozedFor="";return;}
   Recurrence.Refresh(t,today);DateTime? current=Recurrence.Occurrence(t,today);string next=Next(t,today);if(!current.HasValue||!IsDate(next))return;
   Recurrence.ShiftReminder(t,current.Value,Date(next));t.RepeatDue=next;if(t.Due!="")t.Due=next;t.SnoozedUntil="";t.SnoozedFor="";
  }
  public static bool View(Todo t,string view){return View(t,view,DateTime.Today);}
  public static bool View(Todo t,string view,DateTime today){
   if(view=="done")return t.Done;if(t.Done)return false;
   if(view=="today")return Recurrence.DisplayDue(t,today)==Ymd(today);if(view=="overdue")return t.Repeat=="none"&&t.Due!=""&&Date(t.Due)<today.Date;
   if(view=="urgent")return t.Urgency>0;if(view=="very-urgent")return t.Urgency==2;if(view=="important")return t.Importance>0;if(view=="very-important")return t.Importance==2;
   if(view.StartsWith("tag:"))return t.Tags.Contains(view.Substring(4));return true;
  }
 }
}
