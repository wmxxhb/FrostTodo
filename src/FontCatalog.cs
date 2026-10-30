using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FrostTodo {
 public static class FontCatalog {
  public static readonly string[] Keys={"system","yahei","segoe","yozai","marker","cubic","desktop"};
  public static readonly string[] Labels={"系统默认","微软雅黑 UI","Segoe UI · 中文使用微软雅黑","Yozai · 悠哉字体","LXGW Marker Gothic · 霞鹜漫黑","Cubic 11 · 俐方体","GPT 桌面字体"};
  static readonly Dictionary<string,FontFamily> loaded=new Dictionary<string,FontFamily>();
  public static readonly FontFamily SystemFont=new FontFamily(Fonts.SystemFontFamilies.Any(f=>f.Source=="Noto Sans SC")?"Noto Sans SC":"Microsoft YaHei UI");
  public static FontFamily Resolve(string key){
   if(key=="system"||String.IsNullOrEmpty(key))return SystemFont;if(key=="desktop")return new FontFamily("Segoe UI, Global User Interface");FontFamily font;if(loaded.TryGetValue(key,out font))return font;
   if(key=="yahei"||key=="segoe"){font=new FontFamily(key=="yahei"?"Microsoft YaHei UI":"Segoe UI,Microsoft YaHei UI");loaded[key]=font;return font;}
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fonts");if(!Directory.Exists(folder))return SystemFont;
   string family=key=="yozai"?"Yozai":key=="marker"?"LXGW Marker Gothic":key=="cubic"?"Cubic 11":"";
   foreach(FontFamily candidate in Fonts.GetFontFamilies(new Uri(folder+Path.DirectorySeparatorChar,UriKind.Absolute)))if(candidate.FamilyNames.Values.Any(n=>n.Equals(family,StringComparison.OrdinalIgnoreCase))){loaded[key]=candidate;return candidate;}
   return SystemFont;
  }
 }
}
