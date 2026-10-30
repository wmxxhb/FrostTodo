using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FrostTodo {
 public sealed class GptPalette {
  public string Surface,Secondary,Elevated,Ink,Muted,Primary,OnPrimary,Hover,Selected,CheckBorder,ScrollThumb;
 }
 public static class GptTheme {
  static Dictionary<string,string[]> paths;
  public static string ReadAsset(string name){using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(stream==null)throw new InvalidOperationException("Missing theme asset: "+name);using(StreamReader reader=new StreamReader(stream))return reader.ReadToEnd();}}
  public static bool IsDark(Preferences p){
   if(p.GptTheme=="dark")return true;if(p.GptTheme=="system"){try{return Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==0;}catch(Exception){return false;}}
   if(p.GptTheme=="custom"){Color c=(Color)ColorConverter.ConvertFromString(p.Tint);return c.R*.2126+c.G*.7152+c.B*.0722<130;}return false;
  }
  // Neutral tokens adapted from openai/apps-sdk-ui, revision recorded in ThirdParty/.
  public static GptPalette Palette(bool dark){return dark?new GptPalette{Surface="#212121",Secondary="#181818",Elevated="#303030",Ink="#FFFFFF",Muted="#AFAFAF",Primary="#F3F3F3",OnPrimary="#0D0D0D",Hover="#303030",Selected="#393939",CheckBorder="#8F8F8F",ScrollThumb="#5D5D5D"}:new GptPalette{Surface="#FFFFFF",Secondary="#F9F9F9",Elevated="#FFFFFF",Ink="#0D0D0D",Muted="#5D5D5D",Primary="#181818",OnPrimary="#FFFFFF",Hover="#F3F3F3",Selected="#EDEDED",CheckBorder="#8F8F8F",ScrollThumb="#CDCDCD"};}
  public static Color SurfaceColor(Preferences p){return (Color)ColorConverter.ConvertFromString(p.GptTheme=="custom"?p.Tint:Palette(IsDark(p)).Surface);}
  public static void Publish(Preferences p){
   if(Application.Current==null)return;bool dark=IsDark(p);GptPalette palette=Palette(dark);ResourceDictionary r=Application.Current.Resources;
   r["Ink"]=Ui.Brush(p.Foreground=="auto"?palette.Ink:p.Foreground);Color muted=(Color)ColorConverter.ConvertFromString(palette.Muted);r["Muted"]=new SolidColorBrush(Color.FromArgb((byte)Math.Round(p.TextContrast*2.55),muted.R,muted.G,muted.B));
   r["Accent"]=Ui.Brush(palette.Ink);r["Field"]=Ui.Brush(palette.Secondary);r["Elevated"]=Ui.Brush(palette.Elevated);r["Hover"]=Ui.Brush(palette.Hover);r["Selected"]=Ui.Brush(palette.Selected);r["Primary"]=Ui.Brush(palette.Primary);r["OnPrimary"]=Ui.Brush(palette.OnPrimary);r["PrimaryHover"]=Ui.Brush(dark?"#CDCDCD":"#303030");
   r["Line"]=Ui.Brush(dark?"#1FFFFFFF":"#1A0D0D0D");r["FocusRing"]=Ui.Brush("#0285FF");r["CheckBorder"]=Ui.Brush(palette.CheckBorder);r["ScrollThumb"]=Ui.Brush(palette.ScrollThumb);r["Danger"]=Ui.Brush(dark?"#FF8583":"#E02E2A");
   r["UiTabsHeight"]=Math.Max(40,Math.Ceiling(14*p.TextSize/15.0*1.5)+20);r["UiHeaderHeight"]=Math.Max(32,Math.Ceiling(p.TextSize*1.5));
  }
  public static void PaintSurface(Border border,Color color,int opacity,bool nativeTint,Preferences p){
   byte alpha=(byte)Math.Round(Math.Max(0,Math.Min(100,opacity))*2.55);bool dark=IsDark(p);border.CornerRadius=new CornerRadius(0);border.BorderThickness=new Thickness(1);border.BorderBrush=new SolidColorBrush(dark?Color.FromArgb((byte)(alpha*.12),255,255,255):Color.FromArgb((byte)(alpha*.10),13,13,13));border.Background=nativeTint?new SolidColorBrush(Color.FromArgb(1,255,255,255)):new SolidColorBrush(Color.FromArgb(alpha,color.R,color.G,color.B));
  }
  public static void Primary(Button b){if(!UiVariant.IsGpt)return;b.Tag="primary";b.SetResourceReference(Control.ForegroundProperty,"OnPrimary");b.SetResourceReference(Control.BackgroundProperty,"Primary");b.BorderThickness=new Thickness(0);}
  public static void Popup(Border b){if(!UiVariant.IsGpt)return;b.SetResourceReference(Border.BackgroundProperty,"Elevated");b.SetResourceReference(Border.BorderBrushProperty,"Line");b.BorderThickness=new Thickness(1);b.CornerRadius=new CornerRadius(12);}
  public static void StyleCalendar(Calendar calendar){calendar.SetResourceReference(FrameworkElement.StyleProperty,typeof(Calendar));calendar.SetResourceReference(Calendar.CalendarDayButtonStyleProperty,typeof(System.Windows.Controls.Primitives.CalendarDayButton));calendar.SetResourceReference(Calendar.CalendarButtonStyleProperty,typeof(System.Windows.Controls.Primitives.CalendarButton));calendar.SetResourceReference(Calendar.CalendarItemStyleProperty,typeof(System.Windows.Controls.Primitives.CalendarItem));}
  public static FrameworkElement Icon(string glyph,string fill){
   if(paths==null)paths=new JavaScriptSerializer().Deserialize<Dictionary<string,string[]>>(ReadAsset("FrostTodo.GptIcons"));
   string name=glyph=="\uE710"?"Plus":glyph=="\uE71C"?"Filter":glyph=="\uE790"?"SettingsSlider":glyph=="\uE713"?"Settings":glyph=="\uE738"?"ChevronDown":glyph=="\uE712"?"DotsHorizontal":glyph=="\uE72B"?"ArrowLeft":glyph=="\uE74D"?"Trash":glyph=="check"?"Check":"Clock";
   Canvas canvas=new Canvas{Width=24,Height=24};foreach(string path in paths[name]){Geometry geometry=Geometry.Parse("F0 "+path);geometry.Freeze();System.Windows.Shapes.Path shape=new System.Windows.Shapes.Path{Data=geometry};shape.SetResourceReference(Shape.FillProperty,fill);canvas.Children.Add(shape);}return new Viewbox{Width=18,Height=18,Child=canvas,IsHitTestVisible=false};
  }
  public static string RepeatLabel(string repeat){return repeat=="daily"?"每天":repeat=="weekdays"?"工作日":repeat=="weekly"?"每周":repeat=="monthly"?"每月":"指定星期";}
 }
}
