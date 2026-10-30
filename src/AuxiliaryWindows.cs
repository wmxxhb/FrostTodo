using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;

namespace FrostTodo {
 public sealed class DotWindow:Window {
  readonly MainWindow owner;readonly Border circle=new Border();readonly TextBlock number=Ui.Text("",18,false);public bool AllowClose;
  public DotWindow(MainWindow parent){owner=parent;Title="待办悬浮点";Width=48;Height=48;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;
   circle.CornerRadius=new CornerRadius(24);circle.BorderThickness=new Thickness(1);circle.BorderBrush=Ui.Brush("#BEFFFFFF");circle.Margin=new Thickness(3);circle.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=6,ShadowDepth=1,Opacity=.13};number.HorizontalAlignment=HorizontalAlignment.Center;number.VerticalAlignment=VerticalAlignment.Center;number.FontWeight=FontWeights.Medium;number.FontFamily=new FontFamily("Segoe UI Variable Display,Microsoft YaHei UI");circle.Child=number;Content=circle;
   Rect work=SystemParameters.WorkArea;Left=owner.Pref.DotLeft<0?work.Right-62:owner.Pref.DotLeft;Top=owner.Pref.DotTop<0?work.Top+work.Height*.6:owner.Pref.DotTop;if(Left<SystemParameters.VirtualScreenLeft||Left>SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-25){Left=work.Right-62;Top=work.Top+100;}
   MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){double x=Left,y=Top;try{DragMove();}catch(InvalidOperationException){}if(Math.Abs(Left-x)+Math.Abs(Top-y)<4)owner.Restore();else{FormsSnap();owner.DotMoved(Left,Top);}};
   MouseRightButtonUp+=delegate{ContextMenu menu=new ContextMenu();Add(menu,"打开待办",owner.Restore);MenuItem counts=new MenuItem{Header="数字显示"};string[] names={"所有待办","今日待办","逾期待办","重要待办","紧急待办","当前筛选","不显示数字"},values={"all","today","overdue","important","urgent","current","none"};for(int i=0;i<names.Length;i++){string v=values[i];MenuItem item=new MenuItem{Header=names[i],IsCheckable=true,IsChecked=owner.Pref.Count==v};item.Click+=delegate{owner.SetDotCount(v);Refresh();};counts.Items.Add(item);}menu.Items.Add(counts);menu.Items.Add(new Separator());Add(menu,"完全隐藏",delegate{Hide();});Add(menu,"退出",owner.Quit);menu.IsOpen=true;};
   Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!AllowClose){e.Cancel=true;Hide();}};
   Refresh();
  }
  void Add(ContextMenu menu,string name,Action action){MenuItem item=new MenuItem{Header=name};item.Click+=delegate{action();};menu.Items.Add(item);}
  void FormsSnap(){System.Drawing.Rectangle w=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left,(int)Top)).WorkingArea;double scale=PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;double l=w.Left/scale,r=w.Right/scale,t=w.Top/scale,b=w.Bottom/scale;if(Left-l<35)Left=l+6;if(r-(Left+Width)<35)Left=r-Width-6;Top=Math.Max(t,Math.Min(b-Height,Top));}
  public void Refresh(){int n=owner.DotCount();number.Text=n<0?"·":n>99?"99+":n.ToString();number.FontSize=n>99?13:18;Color c=UiVariant.IsGpt?GptTheme.SurfaceColor(owner.Pref):(Color)ColorConverter.ConvertFromString(owner.Pref.Tint);circle.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Max(175,owner.Pref.Opacity*2.55),c.R,c.G,c.B));bool dark=c.R*.2126+c.G*.7152+c.B*.0722<130;number.Foreground=owner.Pref.Foreground=="auto"?Ui.Brush(dark?"#F4F0E9":"#263344"):Ui.Brush(owner.Pref.Foreground);string label=n<0?"展开待办":"展开待办，"+n+"项";AutomationProperties.SetName(this,label);ToolTip=label+" · 右键调整计数";}
 }
 public sealed class ReminderWindow:Window {
  public ReminderWindow(MainWindow main,List<Todo> tasks){
   Title="待办提醒";Width=330;SizeToContent=SizeToContent.Height;MaxHeight=480;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;Background=Brushes.Transparent;if(!UiVariant.IsGpt){Resources["Ink"]=Ui.Brush("#263344");Resources["Muted"]=Ui.Brush("#66778B");}
   System.Windows.Shell.WindowChrome.SetWindowChrome(this,new System.Windows.Shell.WindowChrome{CaptionHeight=0,GlassFrameThickness=new Thickness(-1),CornerRadius=new CornerRadius(0),ResizeBorderThickness=new Thickness(0)});
   StackPanel content=new StackPanel{Margin=new Thickness(17)};TextBlock header=Ui.Text("待办提醒",11,true);header.Margin=new Thickness(0,0,0,12);content.Children.Add(header);
   foreach(Todo t in tasks.Take(4)){Todo captured=t;Button item=Ui.Button("",delegate{main.OpenFromReminder(captured);Close();});TextBlock text=Ui.Text(t.Title,14,false);text.TextWrapping=TextWrapping.Wrap;item.Content=text;item.HorizontalContentAlignment=HorizontalAlignment.Left;item.Margin=new Thickness(-5,0,-5,5);content.Children.Add(item);}if(tasks.Count>4)content.Children.Add(Ui.Text("另有 "+(tasks.Count-4)+" 项提醒",11,true));
   WrapPanel buttons=new WrapPanel{Margin=new Thickness(0,10,0,0)};buttons.Children.Add(Ui.Button("稍后 10 分钟",delegate{main.Snooze(tasks);Close();}));buttons.Children.Add(Ui.Button("关闭",Close));content.Children.Add(buttons);Content=new Border{Background=Ui.Brush("#EAF0F6"),CornerRadius=new CornerRadius(0),BorderThickness=new Thickness(1),BorderBrush=Brushes.Black,Child=content};
   if(UiVariant.IsGpt)GptTheme.Popup((Border)Content);SourceInitialized+=delegate{Native.Square(this);};Loaded+=delegate{Rect area=SystemParameters.WorkArea;Left=area.Right-ActualWidth-18;Top=area.Bottom-ActualHeight-18;};
  }
 }
}
