using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace FrostTodo {
 // The picture belongs to the saved preferences, so moving the original file is harmless.
 public static class BackgroundPicture {
  public const int MaxSide=1600;
  public static string Import(string path){
   if(new FileInfo(path).Length>50L*1024*1024)throw new InvalidOperationException("请选择小于 50 MB 的图片。");
   using(FileStream file=File.OpenRead(path)){
    BitmapDecoder decoder=BitmapDecoder.Create(file,BitmapCreateOptions.DelayCreation,BitmapCacheOption.OnDemand);
    BitmapFrame frame=decoder.Frames[0];int width=frame.PixelWidth,height=frame.PixelHeight;
    if(width<1||height<1||(long)width*height>100000000)throw new InvalidOperationException("图片尺寸过大，请先缩小图片。");
    file.Position=0;BitmapImage image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=file;
    if(Math.Max(width,height)>MaxSide){if(width>=height)image.DecodePixelWidth=MaxSide;else image.DecodePixelHeight=MaxSide;}
    image.EndInit();image.Freeze();PngBitmapEncoder encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
    using(MemoryStream output=new MemoryStream()){encoder.Save(output);return Convert.ToBase64String(output.ToArray());}
   }
  }
  public static BitmapSource Decode(string encoded){
   using(MemoryStream stream=new MemoryStream(Convert.FromBase64String(encoded))){
    BitmapImage image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
   }
  }
 }
 public sealed class BackgroundPictureLayer:Grid {
  public const double BlurRadius=24;
  readonly Border picture=new Border{Margin=new Thickness(-BlurRadius*2)};
  readonly Border tint=new Border();string cached="";
  public BackgroundPictureLayer(){
   IsHitTestVisible=false;ClipToBounds=true;Visibility=Visibility.Collapsed;
   BlurEffect blur=new BlurEffect{Radius=BlurRadius,KernelType=KernelType.Gaussian,RenderingBias=RenderingBias.Quality};blur.Freeze();picture.Effect=blur;
   Children.Add(picture);Children.Add(tint);
  }
  public bool Apply(Preferences settings,Color color){
   string encoded=settings.BackgroundImage??"";
   if(encoded!=cached){cached=encoded;picture.Background=null;if(encoded!=""){try{ImageBrush brush=new ImageBrush(BackgroundPicture.Decode(encoded)){Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center};brush.Freeze();picture.Background=brush;}catch(Exception){/* Fall back to the existing frosted color on a damaged picture. */}}}
   Visibility=picture.Background==null?Visibility.Collapsed:Visibility.Visible;
   Opacity=1;tint.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Max(0,Math.Min(100,settings.Opacity))*2.55),color.R,color.G,color.B));return picture.Background!=null;
  }
 }
}
