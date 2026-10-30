using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace FrostTodo {
 public static class UiVariant {
  public static bool IsGpt{get{
#if GPT_STYLE
   return true;
#else
   return false;
#endif
  }}
  public static string DataFolder{get{return IsGpt?"FrostTodo-GPT":"FrostTodo";}}
  public static int MaximumOpacity{get{return IsGpt?100:95;}}
  public static void Defaults(Preferences p){if(!IsGpt)return;p.Tint="#FFFFFF";p.Foreground="auto";p.Opacity=100;p.LightBackdropV1=true;p.Width=420;p.Height=640;p.Font="segoe";p.TextContrast=100;}
  public static AppData LoadProfile(Store destination,string seedDirectory){
   if(!IsGpt||File.Exists(destination.FilePath)||String.IsNullOrEmpty(seedDirectory))return destination.Load();
   string source=Path.Combine(seedDirectory,"tasks.json");if(!File.Exists(source))return destination.Load();
   AppData original;try{original=destination.Decode(File.ReadAllText(source));}catch(Exception){destination.RecoveryMessage="原版数据无法读取，尚未自动复制；原文件已保留，可稍后导入备份。";return destination.Load();}
   AppData copy=new AppData{Tasks=original.Tasks.Select(t=>t.Copy()).ToList(),Themes=new List<string>(original.Themes)};copy.Settings.QuickViews=new List<string>(original.Settings.QuickViews);copy.Settings.QuickTags=new List<string>(original.Settings.QuickTags);copy.Settings.QuickTagsAll=original.Settings.QuickTagsAll;copy.Settings.Collapse=original.Settings.Collapse;copy.Settings.Count=original.Settings.Count;destination.Save(copy);return copy;
  }
 }
}
