using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace FrostTodo {
 public static class AppIcon {
  static Stream Open(){Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("FrostTodo.AppIcon");if(stream==null)throw new InvalidOperationException("Application icon is missing.");return stream;}
  static BitmapSource LoadWindowIcon(){using(Stream stream=Open()){IconBitmapDecoder decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);BitmapFrame frame=decoder.Frames.OrderByDescending(f=>f.PixelWidth).First();frame.Freeze();return frame;}}
  public static readonly BitmapSource WindowIcon=LoadWindowIcon();
  public static System.Drawing.Icon CreateTrayIcon(){using(Stream stream=Open())using(System.Drawing.Icon icon=new System.Drawing.Icon(stream,32,32))return (System.Drawing.Icon)icon.Clone();}
 }
}
