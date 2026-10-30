using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;

namespace FrostTodo {
 // Reuse the tested task engine; this process has no WPF window or tray.
 public static class ElectronBridge {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=40*1024*1024};
  static Store store;static AppData data;static List<Todo> undo;static string recovery="";static DateTime displayedDay=DateTime.Today;
  [STAThread] public static int Main(string[] args){
   if(args.Contains("--self-test"))return SelfTest.Run(args);
   Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
   if(args.Length<1)return 2;
   try{store=new Store(Path.GetFullPath(args[0]));
    if(!File.Exists(store.FilePath)&&args.Length>1&&File.Exists(Path.Combine(args[1],"tasks.json"))){
     data=store.Decode(File.ReadAllText(Path.Combine(args[1],"tasks.json")));DesktopDefaults(data.Settings);store.Save(data);recovery="已复制原版任务，之后两版独立保存。";
    }else data=store.Load();data.Normalize();if(store.RecoveryMessage!="")recovery=store.RecoveryMessage;
   }catch(Exception e){Console.WriteLine(Json.Serialize(new{fatal=e.Message}));return 1;}
   string line;while((line=Console.ReadLine())!=null){object id=null;try{Dictionary<string,object> request=Json.Deserialize<Dictionary<string,object>>(line);id=request["id"];string op=(string)request["op"];object arg=request.ContainsKey("arg")?request["arg"]:null;object result=Dispatch(op,arg);Console.WriteLine(Json.Serialize(new{id=id,ok=true,result=result}));if(op=="close")break;}catch(Exception e){Console.WriteLine(Json.Serialize(new{id=id,ok=false,error=e.Message}));}}
   return 0;
  }
  static void DesktopDefaults(Preferences p){DesktopAppearance.ResetText(p);}
  static T ConvertTo<T>(object value){return Json.Deserialize<T>(Json.Serialize(value));}
  static Dictionary<string,object> Map(object arg){return arg as Dictionary<string,object>??new Dictionary<string,object>();}
  static string Str(Dictionary<string,object> arg,string key){return arg.ContainsKey(key)?Convert.ToString(arg[key]):"";}
  static Todo Find(string id){Todo t=data.Tasks.FirstOrDefault(x=>x.Id==id);if(t==null)throw new InvalidOperationException("任务已不存在，请刷新后重试。");return t;}
  static object State(){
   Dictionary<string,string[]> views=new Dictionary<string,string[]>();Dictionary<string,string> dates=new Dictionary<string,string>();
   foreach(Todo t in data.Tasks){views[t.Id]=new[]{"all","today","done","overdue","urgent","very-urgent","important","very-important"}.Where(v=>Logic.View(t,v)).ToArray();dates[t.Id]=Recurrence.DisplayDue(t,DateTime.Today);}
   return new{Data=data,Views=views,Dates=dates,Today=Logic.Ymd(DateTime.Today),Recovery=recovery,CanUndo=undo!=null};
  }
  static object Change(Action action,bool taskUndo){
   string before=store.Encode(data);List<Todo> oldUndo=undo;
   try{if(taskUndo)undo=data.Tasks.Select(t=>t.Copy()).ToList();action();data.Normalize();store.Save(data);return State();}
   catch{data=store.Decode(before);undo=oldUndo;throw;}
  }
  static object Dispatch(string op,object argument){
   Dictionary<string,object> a=Map(argument);
   if(op=="snapshot"){if(Recurrence.RefreshAll(data,DateTime.Today))store.Save(data);return State();}
   if(op=="close")return true;
   if(op=="native"){ElectronNative.Apply(new IntPtr(Int64.Parse(Str(a,"handle"))),Str(a,"mode"),Str(a,"tint"),Convert.ToInt32(a["opacity"]));return true;}
   if(op=="export")return store.Encode(data);
   if(op=="resetAppearance")return Change(delegate{DesktopAppearance.Reset(data.Settings);},false);
   if(op=="saveTask")return Change(delegate{
    Todo draft=ConvertTo<Todo>(a["task"]),baseline=a.ContainsKey("baseline")&&a["baseline"]!=null?ConvertTo<Todo>(a["baseline"]):null;
    Todo old=data.Tasks.FirstOrDefault(t=>t.Id==draft.Id);bool editing=baseline!=null;if(editing&&old==null)throw new InvalidOperationException("任务已被删除。");
    if(old!=null&&baseline!=null)Recurrence.MergeEditor(draft,baseline,old);
    if(String.IsNullOrWhiteSpace(draft.Title)||draft.Title.Length>500)throw new InvalidOperationException("请填写 1–500 字的任务标题。");
    if(!Recurrence.ValidTime(draft))throw new InvalidOperationException("时间需要截止日期或重复规则，格式为 18:30。");
    if(!Recurrence.ValidReminder(draft,old,DateTime.Now))throw new InvalidOperationException("请选择将来的提醒日期和时间。");
    if(draft.Repeat=="custom"&&(draft.WeekDays==null||draft.WeekDays.Count==0))throw new InvalidOperationException("至少选择一个星期。");
    draft.Title=draft.Title.Trim();bool reminderChanged=old==null||old.Reminder!=draft.Reminder;draft.ReminderFired=!reminderChanged&&old.ReminderFired;
    if(reminderChanged){draft.SnoozedUntil="";draft.SnoozedFor="";}
    bool changed=old==null||old.Due!=draft.Due||old.Repeat!=draft.Repeat||!old.WeekDays.OrderBy(x=>x).SequenceEqual(draft.WeekDays.OrderBy(x=>x));
    if(changed||reminderChanged&&draft.Due=="")Recurrence.Reset(draft,DateTime.Today);else Recurrence.Refresh(draft,DateTime.Today);
    if(old==null)data.Tasks.Insert(0,draft);else data.Tasks[data.Tasks.IndexOf(old)]=draft;
   },true);
   if(op=="complete")return Change(delegate{Logic.Complete(Find(Str(a,"id")),DateTime.Today);},true);
   if(op=="delete")return Change(delegate{data.Tasks.Remove(Find(Str(a,"id")));},true);
   if(op=="reorder")return Change(delegate{TaskOrdering.Move(data.Tasks,ConvertTo<string[]>(a["visible"]),Str(a,"source"),Str(a,"target"),Convert.ToBoolean(a["after"]));},true);
   if(op=="undo")return Change(delegate{if(undo==null)return;Dictionary<string,Todo> current=data.Tasks.ToDictionary(t=>t.Id);foreach(Todo t in undo){Todo live;current.TryGetValue(t.Id,out live);Recurrence.MergeDeliveryState(t,live,DateTime.Today);}data.Tasks=undo;undo=null;},false);
   if(op=="settings")return Change(delegate{
    Dictionary<string,object> merged=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(data.Settings));
    foreach(var item in a){if(!merged.ContainsKey(item.Key)||item.Key=="BackgroundImage")throw new InvalidOperationException("无效设置项。");merged[item.Key]=item.Value;}data.Settings=ConvertTo<Preferences>(merged);
   },false);
   if(op=="tagAdd")return Change(delegate{if(!Logic.AddTheme(data,Str(a,"tag")))throw new InvalidOperationException("标签为空、重复或超过 16 字。");},false);
   if(op=="tagRemove")return Change(delegate{Logic.RemoveTheme(data,Str(a,"tag"));},true);
   if(op=="tagVisible")return Change(delegate{Logic.SetThemeVisible(data,Str(a,"tag"),Convert.ToBoolean(a["visible"]));},false);
   if(op=="background")return Change(delegate{string file=Str(a,"path");data.Settings.BackgroundImage=file==""?"":BackgroundPicture.Import(file);data.Settings.BackgroundImageName=Path.GetFileName(file);if(file!=""&&data.Settings.Opacity==100)data.Settings.Opacity=55;},false);
   if(op=="import")return Change(delegate{AppData incoming=store.Decode(Str(a,"text"));foreach(Todo t in incoming.Tasks)if(!data.Tasks.Any(x=>x.Id==t.Id))data.Tasks.Add(t);foreach(string tag in incoming.Themes)if(!data.Themes.Contains(tag))data.Themes.Add(tag);},true);
   if(op=="tick"){
    List<Todo> previousTasks=data.Tasks.Select(t=>t.Copy()).ToList();try{bool changed=Recurrence.RefreshAll(data,DateTime.Today)||displayedDay!=DateTime.Today;List<Todo> notices=new List<Todo>();foreach(Todo t in data.Tasks.Where(t=>Recurrence.ReminderDue(t,DateTime.Now))){string cycle=Recurrence.FireReminder(t,DateTime.Now);Todo copy=t.Copy();if(t.Repeat!="none"&&Logic.IsDate(cycle))copy.RepeatDue=cycle;notices.Add(copy);changed=true;}if(changed)store.Save(data);displayedDay=DateTime.Today;return new{Changed=changed,State=changed?State():null,Notices=notices};}catch{data.Tasks=previousTasks;throw;}
   }
   if(op=="snooze")return Change(delegate{foreach(Todo notice in ConvertTo<Todo[]>(a["tasks"])){Todo current=data.Tasks.FirstOrDefault(t=>t.Id==notice.Id);if(current!=null&&!current.Done)Recurrence.Snooze(current,DateTime.Now,notice.Repeat=="none"?"single":notice.RepeatDue);}},false);
   throw new InvalidOperationException("不支持的操作。");
  }
 }
 internal static class ElectronNative {
  delegate bool EnumProc(IntPtr h,IntPtr p);
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,IntPtr p);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetLong(IntPtr h,int index);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder text,int count);
  [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
  [StructLayout(LayoutKind.Sequential)]struct Accent{public int State,Flags;public uint Color;public int Animation;}
  [StructLayout(LayoutKind.Sequential)]struct Composition{public int Attribute;public IntPtr Data,Size;}
  [DllImport("user32.dll")]static extern bool SetWindowCompositionAttribute(IntPtr h,ref Composition d);
  internal static void Apply(IntPtr h,string mode,string tint,int opacity){
   int corner=1,border=unchecked((int)0xFFFFFFFE);DwmSetWindowAttribute(h,33,ref corner,4);DwmSetWindowAttribute(h,34,ref border,4);
   if(mode=="bottom"){
    IntPtr desktop=IntPtr.Zero,after=IntPtr.Zero;uint own;GetWindowThreadProcessId(h,out own);
    EnumWindows(delegate(IntPtr w,IntPtr ignored){if(FindWindowEx(w,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){desktop=w;return false;}return true;},IntPtr.Zero);
    EnumWindows(delegate(IntPtr w,IntPtr ignored){if(w==desktop)return false;if(w==h||!IsWindowVisible(w)||(GetLong(w,-20).ToInt64()&8)!=0)return true;uint pid;GetWindowThreadProcessId(w,out pid);if(pid==own)return true;StringBuilder cls=new StringBuilder(128);GetClassName(w,cls,128);if(cls.ToString()=="Progman"||cls.ToString()=="WorkerW")return true;after=w;return true;},IntPtr.Zero);SetWindowPos(h,after,0,0,0,0,0x13);
   }
   // A full-opacity Chromium surface must not keep a previous acrylic accent.
   Accent policy=new Accent{State=opacity<100?4:0,Color=0};IntPtr mem=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Accent)));
   try{Marshal.StructureToPtr(policy,mem,false);Composition c=new Composition{Attribute=19,Data=mem,Size=new IntPtr(Marshal.SizeOf(typeof(Accent)))};SetWindowCompositionAttribute(h,ref c);}catch(EntryPointNotFoundException){}finally{Marshal.FreeHGlobal(mem);}
  }
 }
}
