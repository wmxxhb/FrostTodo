using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FrostTodo {
 public static partial class SelfTest {
  static double Light(Color c){Func<byte,double> channel=v=>v/255.0<=.04045?v/255.0/12.92:Math.Pow((v/255.0+.055)/1.055,2.4);return channel(c.R)*.2126+channel(c.G)*.7152+channel(c.B)*.0722;}
  static bool Contrast(string a,string b){double x=Light((Color)ColorConverter.ConvertFromString(a)),y=Light((Color)ColorConverter.ConvertFromString(b));return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05)>=4.5;}
  static T Descendant<T>(DependencyObject root) where T:DependencyObject {if(root is T)return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){T result=Descendant<T>(VisualTreeHelper.GetChild(root,i));if(result!=null)return result;}return null;}
  static RenderTargetBitmap Raster(FrameworkElement element,int width,int height){element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();RenderTargetBitmap bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(element);bitmap.Freeze();return bitmap;}
  static void CheckGptTheme(string path,List<string> checks){
   Assert(UiVariant.DataFolder==(UiVariant.IsGpt?"FrostTodo-GPT":"FrostTodo")&&UiVariant.MaximumOpacity==(UiVariant.IsGpt?100:95),"build flavors keep separate profiles and correct opacity limits",checks);if(!UiVariant.IsGpt)return;
   Preferences prefs=new Preferences();Assert(prefs.Tint=="#FFFFFF"&&prefs.Opacity==100&&prefs.Font=="segoe"&&prefs.TextContrast==100,"GPT first launch uses neutral opaque surface and system sans typography",checks);
   AppData normalize=new AppData();normalize.Normalize();Assert(normalize.Settings.Opacity==100,"GPT full opacity survives preferences normalization",checks);
   Ui.SetFont("segoe");List<BitmapSource> previews=new List<BitmapSource>();
   foreach(bool dark in new[]{false,true}){
    prefs.GptTheme=dark?"dark":"light";TextAppearance.Publish(prefs);GptPalette palette=GptTheme.Palette(dark);
    Assert(Contrast(palette.Ink,palette.Surface)&&Contrast(palette.Muted,palette.Surface)&&Contrast(palette.Primary,palette.OnPrimary),"GPT normal, secondary and action text meet contrast requirement in "+prefs.GptTheme,checks);
    Assert(((SolidColorBrush)Application.Current.Resources["Ink"]).Color==(Color)ColorConverter.ConvertFromString(palette.Ink)&&GptTheme.SurfaceColor(prefs)==(Color)ColorConverter.ConvertFromString(palette.Surface),"GPT semantic text and surface switch together in "+prefs.GptTheme,checks);
    StackPanel panel=new StackPanel{Resources=Application.Current.Resources};TextBox input=new TextBox{Text="任务内容",Margin=new Thickness(0,0,0,8)};ComboBox select=Ui.Select(new[]{"每天","每周"},new[]{"daily","weekly"},"daily",null);CheckBox check=new CheckBox{Content="展示重要任务",IsChecked=true};Slider slider=new Slider{Minimum=10,Maximum=100,Value=55};Calendar calendar=new Calendar{DisplayDate=new DateTime(2026,10,30),SelectedDate=new DateTime(2026,10,30)};GptTheme.StyleCalendar(calendar);panel.Children.Add(input);panel.Children.Add(select);panel.Children.Add(check);panel.Children.Add(slider);panel.Children.Add(calendar);Raster(panel,360,700);
    Assert(input.Foreground is SolidColorBrush&&((SolidColorBrush)input.Foreground).Color==(Color)ColorConverter.ConvertFromString(palette.Ink)&&select.Template!=null&&select.Template.FindName("PART_Popup",select) is Popup,"GPT editable and selection controls use theme colors in "+prefs.GptTheme,checks);
    Assert(slider.Template.FindName("PART_Track",slider) is Track&&check.Template!=null&&check.Template.FindName("box",check) is Border,"GPT slider and checkbox retain their native control parts in "+prefs.GptTheme,checks);
    CalendarItem item=Descendant<CalendarItem>(calendar);Grid month=item==null?null:item.Template.FindName("PART_MonthView",item) as Grid;
    Assert(month!=null&&month.Children.Count==49&&calendar.SelectedDate==new DateTime(2026,10,30),"GPT calendar has weekday headings and 42 date cells in "+prefs.GptTheme,checks);
    Border surface=new Border();GptTheme.PaintSurface(surface,GptTheme.SurfaceColor(prefs),100,false,prefs);RenderTargetBitmap flat=Raster(surface,128,128);byte[] pixels=new byte[128*128*4];flat.CopyPixels(pixels,128*4,0);Color c=GptTheme.SurfaceColor(prefs);int at=(64*128+64)*4;Assert(pixels[at]==c.B&&pixels[at+1]==c.G&&pixels[at+2]==c.R&&pixels[at+3]==255,"GPT solid surface stays neutral without diagonal bands in "+prefs.GptTheme,checks);
    previews.Add(GptPreview(prefs));
   }
   foreach(string glyph in new[]{"\uE710","\uE71C","\uE790","\uE713","\uE738","\uE712","\uE72B","\uE74D","\uE917","check"}){FrameworkElement icon=GptTheme.Icon(glyph,"Ink");RenderTargetBitmap image=Raster(icon,24,24);byte[] pixels=new byte[24*24*4];image.CopyPixels(pixels,24*4,0);Assert(Enumerable.Range(0,24*24).Any(i=>pixels[i*4+3]>0),"official SVG path renders as native vector icon "+glyph,checks);}
   prefs.GptTheme="custom";prefs.Tint="#465C47";Assert(GptTheme.IsDark(prefs)&&GptTheme.SurfaceColor(prefs)==(Color)ColorConverter.ConvertFromString("#465C47"),"custom colors retain adaptive GPT control contrast",checks);
   Store original=new Store(Path.Combine(path,"old-profile"));AppData old=new AppData();old.Tasks.Add(new Todo{Id="profile-task",Title="保留旧版任务",Repeat="daily",Tags=new List<string>{"工作"}});old.Settings.Tint="#7B8AD7";old.Settings.QuickViews=new List<string>{"very-important"};original.Save(old);string before=File.ReadAllText(original.FilePath);
   Store fresh=new Store(Path.Combine(path,"gpt-profile"));AppData copied=UiVariant.LoadProfile(fresh,original.DirectoryPath);Assert(copied.Tasks.Count==1&&copied.Tasks[0].Id=="profile-task"&&copied.Settings.Tint=="#FFFFFF"&&copied.Settings.QuickViews.SequenceEqual(new[]{"very-important"})&&File.ReadAllText(original.FilePath)==before,"first GPT launch copies task content and quick filters without changing original data",checks);
   copied.Tasks[0].Title="只改GPT版";fresh.Save(copied);AppData again=UiVariant.LoadProfile(fresh,original.DirectoryPath);Assert(again.Tasks[0].Title=="只改GPT版"&&original.Load().Tasks[0].Title=="保留旧版任务","later GPT launches keep independent edits and do not reseed or overwrite original",checks);
   Store blank=new Store(Path.Combine(path,"explicit-profile"));Assert(UiVariant.LoadProfile(blank,null).Tasks.Count==0,"explicit data directory does not import the personal profile",checks);
   Canvas board=new Canvas{Width=912,Height=708,Background=Ui.Brush("#EDEDED")};for(int i=0;i<previews.Count;i++){Image image=new Image{Source=previews[i],Width=420,Height=640};Canvas.SetLeft(image,24+i*444);Canvas.SetTop(image,44);board.Children.Add(image);TextBlock label=new TextBlock{Text=i==0?"浅色 · 组件预览":"深色 · 组件预览",FontFamily=FontCatalog.Resolve("segoe"),FontSize=13,Foreground=Ui.Brush("#5D5D5D")};Canvas.SetLeft(label,24+i*444);Canvas.SetTop(label,16);board.Children.Add(label);}PngBitmapEncoder encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Raster(board,912,708)));using(FileStream file=File.Create(Path.Combine(path,"GPT-style-preview.png")))encoder.Save(file);
  }
  static BitmapSource GptPreview(Preferences p){
   TextAppearance.Publish(p);GptPalette palette=GptTheme.Palette(GptTheme.IsDark(p));Grid root=new Grid{Width=420,Height=640,Background=Ui.Brush(palette.Surface),Resources=Application.Current.Resources};TextAppearance.Configure(root);StackPanel content=new StackPanel{Margin=new Thickness(20,16,20,16)};root.Children.Add(content);
   Grid header=new Grid{Height=32,Margin=new Thickness(0,0,0,16)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});TextBlock brand=Ui.Text("FrostTodo",13,false);brand.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(brand);StackPanel tools=new StackPanel{Orientation=Orientation.Horizontal};foreach(string glyph in new[]{"\uE710","\uE71C","\uE790","\uE713","\uE738","\uE712"})tools.Children.Add(Ui.Icon(glyph,"预览",delegate{}));Grid.SetColumn(tools,1);header.Children.Add(tools);content.Children.Add(header);
   StackPanel tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(-6,0,0,18)};tabs.Children.Add(Ui.Pill("所有任务",true,delegate{}));tabs.Children.Add(Ui.Pill("今日任务",false,delegate{}));tabs.Children.Add(Ui.Pill("已完成",false,delegate{}));content.Children.Add(tabs);
   string[] titles={"整理今天的三件要事","阅读二十分钟","给植物浇水"},details={"今天  09:30   工作   重要","今天   每天","明天   生活"};for(int i=0;i<3;i++){Grid row=new Grid{Height=76};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});row.ColumnDefinitions.Add(new ColumnDefinition());System.Windows.Shapes.Ellipse circle=new System.Windows.Shapes.Ellipse{Width=18,Height=18,Stroke=Ui.Brush(palette.CheckBorder),StrokeThickness=1.1,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};row.Children.Add(circle);StackPanel text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};text.Children.Add(TextAppearance.Title(titles[i]));TextBlock meta=Ui.Text(details[i],13,true);meta.Margin=new Thickness(0,5,0,0);text.Children.Add(meta);Grid.SetColumn(text,1);row.Children.Add(text);content.Children.Add(row);}
   Border divider=Ui.Divider();divider.Margin=new Thickness(0,16,0,20);content.Children.Add(divider);content.Children.Add(Ui.Text("界面主题",12,true));ComboBox theme=Ui.Select(new[]{"浅色","深色","跟随系统"},new[]{"light","dark","system"},p.GptTheme,null);theme.Margin=new Thickness(0,8,0,20);content.Children.Add(theme);content.Children.Add(Ui.Text("磨砂不透明度",12,true));Slider opacity=new Slider{Minimum=10,Maximum=100,Value=100,Margin=new Thickness(0,8,0,18)};content.Children.Add(opacity);Button done=Ui.Button("完成",delegate{});GptTheme.Primary(done);done.HorizontalAlignment=HorizontalAlignment.Right;content.Children.Add(done);
   return Raster(root,420,640);
  }
 }
}
