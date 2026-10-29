using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace FrostTodo {
 public sealed class MainWindow:Window {
  readonly Store store; AppData data; readonly Grid layout=new Grid();readonly Border surface=new Border();readonly Grid root=new Grid();
  readonly BackgroundPictureLayer backgroundPicture=new BackgroundPictureLayer();
  readonly StackPanel top=new StackPanel();readonly ScrollViewer scroll=new ScrollViewer();readonly StackPanel body=new StackPanel();readonly Grid foot=new Grid();
  readonly Border toastBorder=new Border(); readonly TextBlock toastText=Ui.Text("",11.5,false);readonly Button undoButton;
  readonly DispatcherTimer saveTimer=new DispatcherTimer();readonly DispatcherTimer reminderTimer=new DispatcherTimer();readonly DispatcherTimer toastTimer=new DispatcherTimer();
  Forms.NotifyIcon tray;DotWindow dot;HwndSource hwndSource;Native.ForegroundWatch foregroundWatch;bool quitting,initializing=true,restoringBottom,userCollapsed,isMovingWindow;string page="main",view="all";Todo draft;bool creating;
  readonly HashSet<string> filterTags=new HashSet<string>();readonly HashSet<int> filterUrgency=new HashSet<int>();readonly HashSet<int> filterImportance=new HashSet<int>();string renderedPage="";DateTime displayedDay=DateTime.Today;
  List<Todo> undoTasks;string pendingReminderId="";string hotkey="Ctrl + Alt + 空格";Action undoAction;
  TextBox titleInput;TimePicker timeInput,reminderTime;DatePicker dueInput,reminderInput;ComboBox urgencyInput,importanceInput,repeatInput;TextBlock editError;ScrollViewer quickStrip;double quickOffset;
  readonly string[] levelUrgency={"普通","紧急","非常紧急"},levelImportance={"普通","重要","非常重要"};
  readonly string[] colorNames={"雾白","灰蓝","浅沙","鼠尾草","长春花蓝","可可棕","苔青色","浅松色"};
  readonly string[] colors={"#F3F6FA","#D8E6F2","#F3E8D9","#DFE8DD","#7B8AD7","#6D483B","#465C47","#FFC44F"};
  public MainWindow(Store storage,AppData state){
   store=storage;data=state;data.Normalize();Title="桌面待办 · FrostTodo";Width=data.Settings.Width;Height=data.Settings.Height;MinWidth=300;MinHeight=340;MaxWidth=1600;MaxHeight=1800;
   Icon=AppIcon.WindowIcon;Ui.SetFont(data.Settings.Font);WindowStyle=WindowStyle.None;AllowsTransparency=true;ResizeMode=ResizeMode.NoResize;Background=Brushes.Transparent;ShowInTaskbar=true;ShowActivated=data.Settings.Mode!="bottom";FontFamily=Ui.CurrentFont;UseLayoutRounding=true;SnapsToDevicePixels=true;Language=System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN");
   TextAppearance.Publish(data.Settings);TextAppearance.Configure(this);
   Rect work=SystemParameters.WorkArea;Left=data.Settings.Left<0?work.Right-Width-35:data.Settings.Left;Top=data.Settings.Top<0?work.Top+60:data.Settings.Top;EnsureOnScreen();
   layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   top.Margin=new Thickness(17,10,13,7);layout.Children.Add(top);
   scroll.Content=body;scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;scroll.Padding=new Thickness(16,0,12,0);scroll.PanningMode=PanningMode.VerticalOnly;Grid.SetRow(scroll,1);layout.Children.Add(scroll);
   foot.Margin=new Thickness(17,8,17,11);Grid.SetRow(foot,2);layout.Children.Add(foot);
   surface.CornerRadius=new CornerRadius(0);surface.BorderThickness=new Thickness(1);surface.BorderBrush=Brushes.Transparent;surface.Child=layout;
   root.ClipToBounds=true;root.Children.Add(backgroundPicture);root.Children.Add(surface);Content=root;
   Thumb grip=new Thumb{Width=15,Height=15,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,4,4),Cursor=Cursors.SizeNWSE,Opacity=.35};
   grip.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Thumb'><Grid Background='Transparent'><Path Data='M 4,13 L 13,4 M 9,13 L 13,9' Stroke='#75879B' StrokeThickness='1'/></Grid></ControlTemplate>");
   AutomationProperties.SetName(grip,"调整窗口大小");grip.DragDelta+=delegate(object sender,DragDeltaEventArgs e){Width=Math.Max(MinWidth,Math.Min(MaxWidth,Width+e.HorizontalChange));Height=Math.Max(MinHeight,Math.Min(MaxHeight,Height+e.VerticalChange));};root.Children.Add(grip);
   AddResizeEdge(HorizontalAlignment.Left,VerticalAlignment.Stretch,Cursors.SizeWE,6,Double.NaN,delegate(double dx,double dy){double next=Math.Max(MinWidth,Math.Min(MaxWidth,Width-dx));Left+=Width-next;Width=next;});
   AddResizeEdge(HorizontalAlignment.Right,VerticalAlignment.Stretch,Cursors.SizeWE,6,Double.NaN,delegate(double dx,double dy){Width=Math.Max(MinWidth,Math.Min(MaxWidth,Width+dx));});
   AddResizeEdge(HorizontalAlignment.Stretch,VerticalAlignment.Top,Cursors.SizeNS,Double.NaN,6,delegate(double dx,double dy){double next=Math.Max(MinHeight,Math.Min(MaxHeight,Height-dy));Top+=Height-next;Height=next;});
   AddResizeEdge(HorizontalAlignment.Stretch,VerticalAlignment.Bottom,Cursors.SizeNS,Double.NaN,6,delegate(double dx,double dy){Height=Math.Max(MinHeight,Math.Min(MaxHeight,Height+dy));});
   StackPanel message=new StackPanel{Orientation=Orientation.Horizontal};message.Children.Add(toastText);undoButton=Ui.Button("撤销",delegate{if(undoAction!=null)undoAction();HideToast();});undoButton.Margin=new Thickness(10,-5,-5,-5);message.Children.Add(undoButton);
   toastBorder.Child=message;toastBorder.Background=Ui.Brush("#F0F4F8");toastBorder.BorderBrush=Ui.Brush("#CCD5DF");toastBorder.BorderThickness=new Thickness(1);toastBorder.CornerRadius=new CornerRadius(10);toastBorder.Padding=new Thickness(11,9,11,9);toastBorder.HorizontalAlignment=HorizontalAlignment.Center;toastBorder.VerticalAlignment=VerticalAlignment.Bottom;toastBorder.Margin=new Thickness(12,0,12,48);toastBorder.Visibility=Visibility.Collapsed;root.Children.Add(toastBorder);
   saveTimer.Interval=TimeSpan.FromMilliseconds(450);saveTimer.Tick+=delegate{saveTimer.Stop();SaveNow();};
   reminderTimer.Interval=TimeSpan.FromSeconds(5);reminderTimer.Tick+=delegate{if(displayedDay!=DateTime.Today){displayedDay=DateTime.Today;if(page=="main")Render();}if(dot!=null)dot.Refresh();CheckReminders();};
   toastTimer.Interval=TimeSpan.FromSeconds(7);toastTimer.Tick+=delegate{HideToast();};
   SourceInitialized+=SourceReady;Loaded+=delegate{initializing=false;ApplyAppearance();ApplyMode();Render();reminderTimer.Start();CheckReminders();};
   SizeChanged+=delegate{if(!initializing){data.Settings.Width=Width;data.Settings.Height=Height;ScheduleSave();}};
   Activated+=delegate{if(!isMovingWindow)ApplyAppearance();if(!initializing&&!isMovingWindow&&data.Settings.Mode=="bottom")Native.PlaceBottom(this);};Deactivated+=delegate{if(!isMovingWindow)ApplyAppearance();};
   StateChanged+=delegate{if(!initializing&&!quitting&&!restoringBottom&&data.Settings.Mode=="bottom"&&WindowState==WindowState.Minimized)Dispatcher.BeginInvoke(new Action(KeepBottomVisible));};
   LocationChanged+=delegate{if(!initializing&&IsVisible&&WindowState==WindowState.Normal){data.Settings.Left=Left;data.Settings.Top=Top;ScheduleSave();}};
   Closing+=delegate(object sender,CancelEventArgs e){if(!quitting){e.Cancel=true;Collapse();}else SaveNow();};
   PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){if(page!="main")Main();else Collapse();e.Handled=true;}if(e.Key==Key.N&&Keyboard.Modifiers==ModifierKeys.Control){NewTask();e.Handled=true;}if(e.Key==Key.Enter&&Keyboard.Modifiers==ModifierKeys.Control&&page=="edit"){SaveTask();e.Handled=true;}};
   BuildTray();Render();
  }
  void SourceReady(object sender,EventArgs e){
   Native.Square(this);hwndSource=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);hwndSource.AddHook(WndProc);
   if(!Native.RegisterHotKey(hwndSource.Handle,4101,0x4003,0x20)){hotkey="Ctrl + Alt + T";if(!Native.RegisterHotKey(hwndSource.Handle,4101,0x4003,0x54)){hotkey="Ctrl + Shift + F12";if(!Native.RegisterHotKey(hwndSource.Handle,4101,0x4006,0x7B))hotkey="托盘图标";}}
   if(tray!=null)tray.ContextMenuStrip.Items[0].Text="打开待办  "+hotkey;
   foregroundWatch=new Native.ForegroundWatch(delegate{if(!quitting&&!initializing)Dispatcher.BeginInvoke(new Action(delegate{if(data.Settings.Mode=="bottom"&&!userCollapsed)KeepBottomVisible();}));});
  }
  IntPtr WndProc(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x0231)isMovingWindow=true;if(msg==0x0232){isMovingWindow=false;if(data.Settings.Mode=="bottom")Native.PlaceBottom(this);}if(msg==0x0312&&w.ToInt32()==4101){if(IsVisible)Collapse();else Restore();handled=true;}if(msg==0x020E&&page=="main"&&quickStrip!=null){int delta=(short)((w.ToInt64()>>16)&0xffff);quickStrip.ScrollToHorizontalOffset(quickStrip.HorizontalOffset+delta*.65);handled=true;}return IntPtr.Zero;}
  void AddResizeEdge(HorizontalAlignment horizontal,VerticalAlignment vertical,Cursor cursor,double width,double height,Action<double,double> resize){Thumb edge=new Thumb{HorizontalAlignment=horizontal,VerticalAlignment=vertical,Width=width,Height=height,Cursor=cursor};edge.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Thumb'><Border Background='Transparent'/></ControlTemplate>");edge.DragDelta+=delegate(object sender,DragDeltaEventArgs e){resize(e.HorizontalChange,e.VerticalChange);};root.Children.Add(edge);}
  void BuildTray(){
   tray=new Forms.NotifyIcon{Text="桌面待办",Icon=AppIcon.CreateTrayIcon(),Visible=true};
   Forms.ContextMenuStrip menu=new Forms.ContextMenuStrip();menu.Items.Add("打开待办  Ctrl+Alt+空格",null,delegate{Dispatcher.BeginInvoke(new Action(Restore));});menu.Items.Add("新建任务",null,delegate{Dispatcher.BeginInvoke(new Action(delegate{Restore();NewTask();}));});
   menu.Items.Add("外观设置",null,delegate{Dispatcher.BeginInvoke(new Action(delegate{Restore();Appearance();}));});menu.Items.Add("功能设置",null,delegate{Dispatcher.BeginInvoke(new Action(delegate{Restore();Settings();}));});menu.Items.Add("标签管理",null,delegate{Dispatcher.BeginInvoke(new Action(delegate{Restore();Tags();}));});menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("导出备份…",null,delegate{Dispatcher.BeginInvoke(new Action(Export));});menu.Items.Add("退出",null,delegate{Dispatcher.BeginInvoke(new Action(Quit));});tray.ContextMenuStrip=menu;
   tray.MouseClick+=delegate(object sender,Forms.MouseEventArgs e){if(e.Button==Forms.MouseButtons.Left)Dispatcher.BeginInvoke(new Action(Restore));};
   tray.BalloonTipClicked+=delegate{Dispatcher.BeginInvoke(new Action(delegate{Restore();Todo t=data.Tasks.FirstOrDefault(x=>x.Id==pendingReminderId);if(t!=null)EditTask(t);}));};
  }
  public void EnsureOnScreen(){Rect all=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);if(Left+Width<all.Left+40||Left>all.Right-40||Top+Height<all.Top+40||Top>all.Bottom-40){Rect work=SystemParameters.WorkArea;Left=work.Right-Width-25;Top=work.Top+45;}Top=Math.Max(all.Top,Top);}
  void ScheduleSave(){saveTimer.Stop();saveTimer.Start();}
  void SaveNow(){try{store.Save(data);}catch(Exception e){Toast("保存失败："+e.Message,null);}}
  public void Toast(string text,Action undo){toastText.Text=text;toastText.Foreground=Ui.Brush("#263344");toastText.MaxWidth=Math.Max(150,ActualWidth-130);undoAction=undo;undoButton.Visibility=undo==null?Visibility.Collapsed:Visibility.Visible;toastBorder.Visibility=Visibility.Visible;toastTimer.Stop();toastTimer.Start();}
  void HideToast(){toastTimer.Stop();toastBorder.Visibility=Visibility.Collapsed;undoAction=null;}
  void Snapshot(){undoTasks=data.Tasks.Select(t=>t.Copy()).ToList();}
  void Undo(){if(undoTasks!=null){data.Tasks=undoTasks;undoTasks=null;SaveNow();Render();}}
  void ApplyAppearance(){
   Ui.SetFont(data.Settings.Font);FontFamily=Ui.CurrentFont;TextAppearance.Publish(data.Settings);
   Color c=(Color)ColorConverter.ConvertFromString(data.Settings.Tint);bool dark=(c.R*.2126+c.G*.7152+c.B*.0722)<130;
   Color ink=data.Settings.Foreground=="auto"?(Color)ColorConverter.ConvertFromString(dark?"#F4F0E9":"#263344"):(Color)ColorConverter.ConvertFromString(data.Settings.Foreground);
   Application.Current.Resources["Ink"]=new SolidColorBrush(ink);Application.Current.Resources["Muted"]=new SolidColorBrush(Color.FromArgb((byte)Math.Round(data.Settings.TextContrast*2.55),ink.R,ink.G,ink.B));
   Application.Current.Resources["Accent"]=new SolidColorBrush(ink);Application.Current.Resources["Field"]=Ui.Brush(dark?"#25202A35":"#52FFFFFF");Application.Current.Resources["Hover"]=Ui.Brush(dark?"#26FFFFFF":"#1B60738C");Application.Current.Resources["Selected"]=Ui.Brush(dark?"#38FFFFFF":"#247A90B0");
   bool nativeTint=hwndSource!=null&&Native.Glass(this,c,data.Settings.Opacity);bool hasPicture=backgroundPicture.Apply(data.Settings,c);root.Background=!nativeTint&&hasPicture?new SolidColorBrush(Color.FromArgb((byte)Math.Round(data.Settings.Opacity*2.55),c.R,c.G,c.B)):Brushes.Transparent;Ui.PaintWindowSurface(surface,c,data.Settings.Opacity,nativeTint||hasPicture);if(dot!=null)dot.Refresh();
  }
  void ApplyMode(){
   if(hwndSource==null)return;Topmost=data.Settings.Mode=="top";ShowInTaskbar=true;ShowActivated=data.Settings.Mode!="bottom";
   if(data.Settings.Mode=="bottom")KeepBottomVisible();else Native.Layer(this,data.Settings.Mode);ApplyAppearance();
  }
  void KeepBottomVisible(){if(restoringBottom||quitting||userCollapsed||isMovingWindow)return;restoringBottom=true;try{if(!IsVisible)Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;EnsureOnScreen();Native.RestoreBottom(this);}finally{restoringBottom=false;}}
  public void Restore(){userCollapsed=false;if(dot!=null)dot.Hide();Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;ApplyMode();if(data.Settings.Mode!="bottom")Activate();Render();}
  void Collapse(){if(page=="edit"){if(!ConfirmDiscard())return;page="main";draft=null;}userCollapsed=true;Hide();if(data.Settings.Collapse=="dot"){if(dot==null)dot=new DotWindow(this);dot.Refresh();dot.Show();}else if(dot!=null)dot.Hide();SaveNow();}
  public void Quit(){if(page=="edit"&&!ConfirmDiscard())return;quitting=true;saveTimer.Stop();reminderTimer.Stop();SaveNow();if(foregroundWatch!=null)foregroundWatch.Dispose();if(dot!=null){dot.AllowClose=true;dot.Close();}if(hwndSource!=null)Native.UnregisterHotKey(hwndSource.Handle,4101);tray.Visible=false;tray.Dispose();Close();Application.Current.Shutdown();}
  bool ConfirmDiscard(){if(page!="edit")return true;ReadEditor();if(draft==null)return true;Todo old=data.Tasks.FirstOrDefault(t=>t.Id==draft.Id);System.Web.Script.Serialization.JavaScriptSerializer json=new System.Web.Script.Serialization.JavaScriptSerializer();bool dirty=creating?(!String.IsNullOrWhiteSpace(draft.Title)||draft.Due!=""||draft.Reminder!=""):old!=null&&json.Serialize(old)!=json.Serialize(draft);if(dirty)return MessageBox.Show(this,"这条任务尚未保存，要放弃修改吗？","未保存的任务",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;return true;}
  void Main(){if(!ConfirmDiscard())return;page="main";draft=null;Render();}
  bool Matches(Todo t){return Logic.View(t,view)&&(filterTags.Count==0||t.Tags.Any(x=>filterTags.Contains(x)))&&(filterUrgency.Count==0||filterUrgency.Contains(t.Urgency))&&(filterImportance.Count==0||filterImportance.Contains(t.Importance));}
  void Render(){
   double offset=(page==renderedPage&&(page=="settings"||page=="appearance"||page=="tags"||page=="edit"))?scroll.VerticalOffset:0;renderedPage=page;
   top.Children.Clear();body.Children.Clear();foot.Children.Clear();scroll.ScrollToTop();
   Grid bar=new Grid{Background=Brushes.Transparent};bar.SetResourceReference(FrameworkElement.HeightProperty,"UiHeaderHeight");bar.ColumnDefinitions.Add(new ColumnDefinition());bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   if(page=="main"){
    TextBlock grip=Ui.Text("···",13,true);grip.Opacity=.55;grip.Margin=new Thickness(2,-3,0,0);grip.VerticalAlignment=VerticalAlignment.Center;bar.Children.Add(grip);
   }else{string title=page=="appearance"?"外观设置":page=="settings"?"功能设置":page=="tags"?"标签管理":page=="filter"?"组合筛选":creating?"新建任务":"编辑任务";TextBlock h=Ui.Text(title,15,false);h.SetResourceReference(TextBlock.FontWeightProperty,"UiEmphasisWeight");h.VerticalAlignment=VerticalAlignment.Center;bar.Children.Add(h);}
   bar.Cursor=Cursors.SizeAll;bar.ToolTip="拖动移动窗口";bar.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){DependencyObject source=e.OriginalSource as DependencyObject;while(source!=null&&source!=bar){if(source is ButtonBase||source is TextBoxBase||source is Selector||source is Thumb)return;source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);}if(e.LeftButton!=MouseButtonState.Pressed)return;Mouse.Capture(null);e.Handled=true;isMovingWindow=true;try{DragMove();}catch(InvalidOperationException){}finally{isMovingWindow=false;Mouse.Capture(null);if(data.Settings.Mode=="bottom")Native.PlaceBottom(this);}};
   StackPanel controls=new StackPanel{Orientation=Orientation.Horizontal,Cursor=Cursors.Arrow};Grid.SetColumn(controls,1);bar.Children.Add(controls);
   if(page=="main"){controls.Children.Add(Ui.Icon("\uE710","新建任务",NewTask));controls.Children.Add(Ui.Icon("\uE71C","组合筛选",FilterPage));controls.Children.Add(Ui.Icon("\uE790","外观设置",Appearance));controls.Children.Add(Ui.Icon("\uE713","功能设置",Settings));controls.Children.Add(Ui.Icon("\uE738","收起",Collapse));controls.Children.Add(Ui.Icon("\uE712","更多选项",OpenMenu));}
   else controls.Children.Add(Ui.Icon("\uE72B","返回",Main));top.Children.Add(bar);
   if(page=="main")RenderMain();else if(page=="edit")RenderEditor();else if(page=="settings")RenderSettings();else if(page=="appearance")RenderAppearance();else if(page=="tags")RenderTags();else RenderFilter();
   ApplyAppearance();if(dot!=null)dot.Refresh();if(offset>0)Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate{scroll.ScrollToVerticalOffset(offset);}));
  }
  void RenderMain(){
   StackPanel tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,0)};ScrollViewer strip=new ScrollViewer{Content=tabs,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.HorizontalOnly,CanContentScroll=false,Height=34,Margin=new Thickness(-4,5,-1,0),ToolTip="左右拖动或滚动查看标签"};strip.SetResourceReference(FrameworkElement.HeightProperty,"UiTabsHeight");quickStrip=strip;top.Children.Add(strip);double savedOffset=quickOffset;
   strip.ScrollChanged+=delegate{if(Object.ReferenceEquals(strip,quickStrip))quickOffset=strip.HorizontalOffset;};
   strip.PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e){strip.ScrollToHorizontalOffset(strip.HorizontalOffset-e.Delta*.65);e.Handled=true;};
   Point start=new Point();double startOffset=0;bool pressed=false,dragged=false;
   strip.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){start=e.GetPosition(strip);startOffset=strip.HorizontalOffset;pressed=true;dragged=false;};
   strip.PreviewMouseMove+=delegate(object sender,MouseEventArgs e){if(e.LeftButton!=MouseButtonState.Pressed){pressed=false;dragged=false;if(strip.IsMouseCaptured)strip.ReleaseMouseCapture();return;}if(!pressed)return;double dx=e.GetPosition(strip).X-start.X;if(!dragged&&Math.Abs(dx)>6){dragged=true;strip.CaptureMouse();}if(dragged){strip.ScrollToHorizontalOffset(startOffset-dx);e.Handled=true;}};
   strip.PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){pressed=false;if(dragged){strip.ReleaseMouseCapture();e.Handled=true;}dragged=false;};
   strip.LostMouseCapture+=delegate{pressed=false;dragged=false;};strip.Unloaded+=delegate{pressed=false;dragged=false;if(strip.IsMouseCaptured)strip.ReleaseMouseCapture();};
   Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate{if(Object.ReferenceEquals(strip,quickStrip))strip.ScrollToHorizontalOffset(savedOffset);}));
   AddQuick(tabs,"所有任务","all");AddQuick(tabs,"今日任务","today");AddQuick(tabs,"已完成","done");if(data.Settings.QuickViews.Contains("urgent"))AddQuick(tabs,"紧急","urgent");if(data.Settings.QuickViews.Contains("very-urgent"))AddQuick(tabs,"非常紧急","very-urgent");if(data.Settings.QuickViews.Contains("important"))AddQuick(tabs,"重要","important");if(data.Settings.QuickViews.Contains("very-important"))AddQuick(tabs,"非常重要","very-important");
   foreach(string tag in data.Themes.Where(x=>data.Settings.QuickTagsAll||data.Settings.QuickTags.Contains(x)))AddQuick(tabs,tag,"tag:"+tag);
   if(filterTags.Count+filterUrgency.Count+filterImportance.Count>0){Button clear=Ui.Button("清除组合条件 ×",delegate{filterTags.Clear();filterUrgency.Clear();filterImportance.Clear();Render();});TextAppearance.BindSize(clear,10.5);clear.HorizontalAlignment=HorizontalAlignment.Left;top.Children.Add(clear);}
   List<Todo> shown=data.Tasks.Where(Matches).ToList();
   if(shown.Count==0){StackPanel empty=new StackPanel{Margin=new Thickness(20,60,20,35)};TextBlock line=Ui.Text(data.Tasks.Count==0?"留一点位置，给接下来要做的事。":"这里暂时没有任务",12.5,true);line.TextAlignment=TextAlignment.Center;empty.Children.Add(line);body.Children.Add(empty);}else foreach(Todo t in shown)AddTaskRow(t);
   TextBlock count=Ui.Text(shown.Count+" 项",10.5,true);count.HorizontalAlignment=HorizontalAlignment.Right;count.VerticalAlignment=VerticalAlignment.Center;count.Margin=new Thickness(0,0,13,0);foot.Children.Add(count);
  }
  void AddQuick(Panel p,string label,string key){p.Children.Add(Ui.Pill(label,view==key,delegate{view=key;Render();}));}
  void AddTaskRow(Todo t){
   Grid row=new Grid{MinHeight=data.Settings.Density=="compact"?46:data.Settings.Density=="spacious"?87:64,Margin=new Thickness(1,0,0,0)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(33)});row.ColumnDefinitions.Add(new ColumnDefinition());
   Button check=Ui.Button("",delegate{Snapshot();Logic.Complete(t,DateTime.Today);SaveNow();Render();Toast(t.Repeat=="none"?(t.Done?"任务已完成":"任务已恢复"):"本次已完成，下次 "+Logic.DateLabel(t.Due),Undo);});
   System.Windows.Shapes.Ellipse circle=new System.Windows.Shapes.Ellipse{Width=18,Height=18,StrokeThickness=1.1};circle.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"Muted");if(t.Done)circle.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,"Muted");check.Content=circle;check.Width=28;check.Height=32;check.Padding=new Thickness(0);check.HorizontalAlignment=HorizontalAlignment.Left;check.VerticalAlignment=VerticalAlignment.Center;AutomationProperties.SetName(check,(t.Done?"恢复：":"完成：")+t.Title);row.Children.Add(check);
   Button edit=Ui.Button("",delegate{EditTask(t);});edit.SetResourceReference(Control.PaddingProperty,"TaskTextPadding");edit.HorizontalContentAlignment=HorizontalAlignment.Stretch;edit.VerticalContentAlignment=VerticalAlignment.Center;Grid.SetColumn(edit,1);AutomationProperties.SetName(edit,"编辑："+t.Title);
   StackPanel content=new StackPanel();SpacedTitle title=TextAppearance.Title(t.Title);title.ToolTip=t.Title;if(t.Done){title.TextDecorations=TextDecorations.Strikethrough;title.Opacity=.55;}content.Children.Add(title);
   List<string> meta=new List<string>();if(t.Tags.Count>0)meta.Add(String.Join(" · ",t.Tags.Take(2)));if(t.Urgency>0)meta.Add(t.Urgency==2?"!!":"!");if(t.Due!="")meta.Add(Logic.DateLabel(t.Due)+(t.Time!=""?" "+t.Time:""));if(t.Importance>0)meta.Add(t.Importance==2?"◆◆":"◆");if(t.Repeat!="none")meta.Add("↻");if(t.Reminder!="")meta.Add("◦");
   if(meta.Count>0){TextBlock sub=Ui.Text(String.Join("   ",meta),11.5,true);sub.TextWrapping=TextWrapping.NoWrap;sub.TextTrimming=TextTrimming.CharacterEllipsis;sub.SetResourceReference(FrameworkElement.MarginProperty,"TaskMetaGap");content.Children.Add(sub);}
   edit.Content=content;row.Children.Add(edit);Border line=new Border{BorderThickness=new Thickness(0,0,0,.5),Child=row};line.SetResourceReference(Border.BorderBrushProperty,"Line");body.Children.Add(line);
  }
  void NewTask(){if(!ConfirmDiscard())return;creating=true;draft=new Todo();if(view.StartsWith("tag:"))draft.Tags.Add(view.Substring(4));if(view=="today")draft.Due=Logic.Ymd(DateTime.Today);if(view=="urgent")draft.Urgency=1;if(view=="very-urgent")draft.Urgency=2;if(view=="important")draft.Importance=1;if(view=="very-important")draft.Importance=2;page="edit";Render();Dispatcher.BeginInvoke(new Action(delegate{titleInput.Focus();}));}
  void EditTask(Todo t){creating=false;draft=t.Copy();page="edit";Render();}
  void RenderEditor(){
   titleInput=new TextBox{Text=draft.Title,MaxLength=160,TextWrapping=TextWrapping.Wrap,MinHeight=42,AcceptsReturn=false,FontSize=14};TextAppearance.BindSize(titleInput,14);body.Children.Add(Ui.Field("任务",titleInput));
   WrapPanel tags=new WrapPanel();foreach(string tag in data.Themes){string captured=tag;tags.Children.Add(Ui.Pill(tag,draft.Tags.Contains(tag),delegate{ReadEditor();if(draft.Tags.Contains(captured))draft.Tags.Remove(captured);else draft.Tags.Add(captured);Render();}));}body.Children.Add(Ui.Field("主题",tags));
   Grid newTag=new Grid{Margin=new Thickness(0,0,0,13)};newTag.ColumnDefinitions.Add(new ColumnDefinition());newTag.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});TextBox input=new TextBox{MaxLength=16,MinHeight=30,FontSize=12};TextAppearance.BindSize(input,12);AutomationProperties.SetName(input,"新主题名称");newTag.Children.Add(input);Button create=Ui.Button("＋ 标签",delegate{string name=input.Text.Trim();if(name=="")return;ReadEditor();if(!data.Themes.Contains(name))data.Themes.Add(name);if(!draft.Tags.Contains(name))draft.Tags.Add(name);ScheduleSave();Render();});TextAppearance.BindSize(create,11);Grid.SetColumn(create,1);newTag.Children.Add(create);body.Children.Add(newTag);
   urgencyInput=Ui.Select(levelUrgency,new[]{"0","1","2"},draft.Urgency.ToString(),null);importanceInput=Ui.Select(levelImportance,new[]{"0","1","2"},draft.Importance.ToString(),null);body.Children.Add(Pair(Ui.Field("紧急程度",urgencyInput),Ui.Field("重要程度",importanceInput)));
   dueInput=new ClearableDatePicker{SelectedDate=draft.Due==""?(DateTime?)null:Logic.Date(draft.Due),SelectedDateFormat=DatePickerFormat.Short,MinWidth=100};timeInput=new TimePicker("截止时间",draft.Time);dueInput.SelectedDateChanged+=delegate{if(!dueInput.SelectedDate.HasValue)timeInput.Value="";};body.Children.Add(Pair(Ui.Field("截止日期 · 可选",dueInput),Ui.Field("时间 · 可选",timeInput)));
   reminderInput=new ClearableDatePicker{SelectedDate=draft.Reminder==""?(DateTime?)null:Logic.Stamp(draft.Reminder).Date,SelectedDateFormat=DatePickerFormat.Short,MinWidth=100};reminderTime=new TimePicker("提醒时间",draft.Reminder==""?"":Logic.Stamp(draft.Reminder).ToString("HH:mm"));reminderInput.SelectedDateChanged+=delegate{if(!reminderInput.SelectedDate.HasValue)reminderTime.Value="";};body.Children.Add(Pair(Ui.Field("提醒日期 · 可选",reminderInput),Ui.Field("提醒时间 · 可选",reminderTime)));
   repeatInput=Ui.Select(new[]{"不重复","每天","工作日","每周","每月","指定星期"},Logic.Repeats,draft.Repeat,delegate(string value){ReadEditor();draft.Repeat=value;Render();});body.Children.Add(Ui.Field("重复 · 可选",repeatInput));
   if(draft.Repeat=="custom"){WrapPanel days=new WrapPanel();string[] names={"日","一","二","三","四","五","六"};for(int i=1;i<=7;i++){int d=i%7;days.Children.Add(Ui.Pill("周"+names[d],draft.WeekDays.Contains(d),delegate{ReadEditor();if(draft.WeekDays.Contains(d))draft.WeekDays.Remove(d);else draft.WeekDays.Add(d);Render();}));}body.Children.Add(days);}
   editError=Ui.Text("",11,false);editError.Foreground=Ui.Brush("#B05A45");body.Children.Add(editError);
   Button cancel=Ui.Button(creating?"取消":"删除",delegate{if(creating)Main();else DeleteTask();});cancel.HorizontalAlignment=HorizontalAlignment.Left;foot.Children.Add(cancel);
   Button save=Ui.Button(creating?"创建任务":"保存",SaveTask);save.Background=Ui.Brush("#28FFFFFF");save.BorderBrush=Ui.Brush("#3C8294AA");save.BorderThickness=new Thickness(1);save.Padding=new Thickness(13,6,13,6);save.HorizontalAlignment=HorizontalAlignment.Right;foot.Children.Add(save);
  }
  Grid Pair(FrameworkElement left,FrameworkElement right){Grid p=new Grid();p.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.4,GridUnitType.Star)});p.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});left.Margin=new Thickness(0,0,8,0);Grid.SetColumn(right,1);p.Children.Add(left);p.Children.Add(right);return p;}
  void ReadEditor(){if(page!="edit"||draft==null||titleInput==null||repeatInput==null)return;draft.Title=titleInput.Text;draft.Due=dueInput.SelectedDate.HasValue?Logic.Ymd(dueInput.SelectedDate.Value):"";draft.Time=timeInput.Value;draft.Urgency=Int32.Parse(Ui.Value(urgencyInput));draft.Importance=Int32.Parse(Ui.Value(importanceInput));draft.Repeat=Ui.Value(repeatInput);draft.Reminder=reminderInput.SelectedDate.HasValue?Logic.Ymd(reminderInput.SelectedDate.Value)+"T"+reminderTime.Value:"";}
  void SaveTask(){
   ReadEditor();if(String.IsNullOrWhiteSpace(draft.Title)){editError.Text="先写下要做的事";titleInput.Focus();return;}if(draft.Time!=""&&(draft.Due==""||!Logic.IsTime(draft.Time))){editError.Text="请选截止日期，并使用 18:30 这样的时间格式";return;}
   if(reminderTime.Value!=""&&!reminderInput.SelectedDate.HasValue){editError.Text="请同时选择提醒日期";return;}if(draft.Reminder!=""&&(!Logic.IsStamp(draft.Reminder)||Logic.Stamp(draft.Reminder)<=DateTime.Now)){editError.Text="请选择未来的提醒时间，例如 18:00";return;}if(draft.Repeat=="custom"&&draft.WeekDays.Count==0){editError.Text="至少选择一个星期";return;}
   draft.Title=draft.Title.Trim();Todo old=data.Tasks.FirstOrDefault(t=>t.Id==draft.Id);if(old==null||old.Reminder!=draft.Reminder)draft.ReminderFired=false;if(old==null||old.Due!=draft.Due||old.Repeat!=draft.Repeat)draft.Anchor=draft.Due==""?DateTime.Today.Day:Logic.Date(draft.Due).Day;
   if(creating)data.Tasks.Insert(0,draft.Copy());else{int index=data.Tasks.FindIndex(t=>t.Id==draft.Id);if(index>=0)data.Tasks[index]=draft.Copy();}
   bool newlyCreated=creating;page="main";draft=null;if(newlyCreated){view="all";filterTags.Clear();filterUrgency.Clear();filterImportance.Clear();}SaveNow();Render();
  }
  void DeleteTask(){Snapshot();data.Tasks.RemoveAll(t=>t.Id==draft.Id);page="main";draft=null;SaveNow();Render();Toast("任务已删除",Undo);}
  void FilterPage(){page="filter";Render();}
  void RenderFilter(){
   WrapPanel tags=new WrapPanel();foreach(string tag in data.Themes){string s=tag;tags.Children.Add(Ui.Pill(s,filterTags.Contains(s),delegate{if(!filterTags.Add(s))filterTags.Remove(s);Render();}));}body.Children.Add(Ui.Field("主题",tags));
   WrapPanel urgency=new WrapPanel(),importance=new WrapPanel();for(int i=0;i<3;i++){int value=i;urgency.Children.Add(Ui.Pill(levelUrgency[i],filterUrgency.Contains(i),delegate{if(!filterUrgency.Add(value))filterUrgency.Remove(value);Render();}));importance.Children.Add(Ui.Pill(levelImportance[i],filterImportance.Contains(i),delegate{if(!filterImportance.Add(value))filterImportance.Remove(value);Render();}));}body.Children.Add(Ui.Field("紧急程度",urgency));body.Children.Add(Ui.Field("重要程度",importance));body.Children.Add(Ui.Text("同类满足任一，不同类同时满足。",11,true));
   Button clear=Ui.Button("清除条件",delegate{filterTags.Clear();filterUrgency.Clear();filterImportance.Clear();Render();});clear.HorizontalAlignment=HorizontalAlignment.Left;foot.Children.Add(clear);Button apply=Ui.Button("查看 "+data.Tasks.Count(Matches)+" 项任务",delegate{page="main";Render();});apply.HorizontalAlignment=HorizontalAlignment.Right;foot.Children.Add(apply);
  }
  void Settings(){page="settings";Render();}
  void Appearance(){page="appearance";Render();}
  void Tags(){page="tags";Render();}
  void RenderSettings(){
   body.Children.Add(Ui.Field("窗口模式",Ui.Select(new[]{"保持置顶","普通窗口","保持置底"},new[]{"top","normal","bottom"},data.Settings.Mode,delegate(string s){data.Settings.Mode=s;ApplyMode();ScheduleSave();})));
   body.Children.Add(Ui.Field("收起方式",Ui.Select(new[]{"悬浮小点","完全隐藏"},new[]{"dot","hidden"},data.Settings.Collapse,delegate(string s){data.Settings.Collapse=s;ScheduleSave();})));
   List<string> countLabels=new List<string>{"所有待办","今日待办","逾期待办","重要待办","紧急待办","当前筛选","不显示数字"},countValues=new List<string>{"all","today","overdue","important","urgent","current","none"};foreach(string tag in data.Themes){countLabels.Add(tag+"主题待办");countValues.Add("tag:"+tag);}
   body.Children.Add(Ui.Field("悬浮点数字",Ui.Select(countLabels.ToArray(),countValues.ToArray(),data.Settings.Count,delegate(string s){data.Settings.Count=s;if(dot!=null)dot.Refresh();ScheduleSave();})));
   Button tags=Ui.Button("标签管理  ›",Tags);tags.HorizontalAlignment=HorizontalAlignment.Left;tags.Margin=new Thickness(-7,3,0,12);body.Children.Add(tags);
   body.Children.Add(Ui.Divider());WrapPanel backups=new WrapPanel();backups.Children.Add(Ui.Button("导出备份…",Export));backups.Children.Add(Ui.Button("导入备份…",Import));body.Children.Add(backups);
   TextBlock help=Ui.Text(hotkey+"唤回或收起\n关闭窗口后留在托盘；选择“退出”才结束运行。",10.5,true);help.Margin=new Thickness(0,10,0,8);body.Children.Add(help);
   Button quit=Ui.Button("退出应用",Quit);quit.HorizontalAlignment=HorizontalAlignment.Left;foot.Children.Add(quit);Button done=Ui.Button("完成",Main);done.HorizontalAlignment=HorizontalAlignment.Right;foot.Children.Add(done);
  }
  void RenderAppearance(){
   body.Children.Add(Ui.Field("界面字体",Ui.Select(FontCatalog.Labels,FontCatalog.Keys,data.Settings.Font,delegate(string key){data.Settings.Font=key;ApplyAppearance();ScheduleSave();})));
   RenderTextSettings();
   body.Children.Add(Ui.Field("信息密度",Ui.Select(new[]{"紧凑","适中（默认）","舒展"},new[]{"compact","balanced","spacious"},data.Settings.Density,delegate(string s){data.Settings.Density=s;ScheduleSave();})));
   body.Children.Add(Ui.Divider());body.Children.Add(Ui.Field("配色搭配",Ui.Select(new[]{"自选配色","柔雾蓝 · 自动字体","长春花蓝 × 浅松色","可可棕 × 浅松色","苔青色 × 浅松色","浅松色 × 可可棕"},new[]{"custom","mist","periwinkle","cocoa","moss","pine"},"custom",delegate(string s){if(s=="custom")return;data.Settings.Tint=s=="mist"?"#D8E6F2":s=="periwinkle"?"#7B8AD7":s=="cocoa"?"#6D483B":s=="moss"?"#465C47":"#FFC44F";data.Settings.Foreground=s=="mist"?"auto":s=="pine"?"#6D483B":"#FFC44F";ApplyAppearance();ScheduleSave();Render();})));
   AddPalette("背景底色",false);AddPalette("字体颜色",true);
   StackPanel pictureTools=new StackPanel{Orientation=Orientation.Horizontal};
   pictureTools.Children.Add(Ui.Button(String.IsNullOrEmpty(data.Settings.BackgroundImage)?"导入图片…":"更换图片…",ImportBackground));
   if(!String.IsNullOrEmpty(data.Settings.BackgroundImage))pictureTools.Children.Add(Ui.Button("移除图片",delegate{data.Settings.BackgroundImage="";data.Settings.BackgroundImageName="";ApplyAppearance();SaveNow();Render();}));
   body.Children.Add(Ui.Field("背景图片",pictureTools));
   TextBlock pictureHint=Ui.Text(String.IsNullOrEmpty(data.Settings.BackgroundImage)?"支持 JPG、PNG、BMP；图片自动铺满并保持磨砂模糊。":"已导入："+(data.Settings.BackgroundImageName??"背景图片")+" · 图片透明度固定，仅调节上层磨砂",11,true);pictureHint.Margin=new Thickness(0,-6,0,14);body.Children.Add(pictureHint);
   TextBlock opacityLabel=Ui.Text("磨砂不透明度  "+data.Settings.Opacity+"%",11.5,true);opacityLabel.Margin=new Thickness(0,0,0,6);body.Children.Add(opacityLabel);Slider opacity=new Slider{Minimum=10,Maximum=95,Value=data.Settings.Opacity,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,4,0,16)};AutomationProperties.SetName(opacity,"磨砂不透明度");opacity.ValueChanged+=delegate{data.Settings.Opacity=(int)opacity.Value;opacityLabel.Text="磨砂不透明度  "+data.Settings.Opacity+"%";ApplyAppearance();ScheduleSave();};body.Children.Add(opacity);
   Button done=Ui.Button("完成",Main);done.HorizontalAlignment=HorizontalAlignment.Right;foot.Children.Add(done);
  }
  void RenderTextSettings(){
   Border preview=new Border{BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Margin=new Thickness(0,0,0,15)};preview.SetResourceReference(Border.BackgroundProperty,"Field");preview.SetResourceReference(Border.BorderBrushProperty,"Line");
   StackPanel lines=new StackPanel();SpacedTitle example=TextAppearance.Title("把重要的事，慢慢做好 Aa 123");lines.Children.Add(example);TextBlock detail=Ui.Text("今天  09:30 · 工作 · 重要",11.5,true);detail.SetResourceReference(FrameworkElement.MarginProperty,"TaskMetaGap");lines.Children.Add(detail);preview.Child=lines;preview.Margin=new Thickness(0,0,0,0);StackPanel previewField=Ui.Field("文字预览",preview);previewField.Margin=new Thickness(0,10,0,0);top.Children.Add(previewField);
   AddTextSlider("字号（任务正文）",12,22,data.Settings.TextSize,1,"",delegate(double value){data.Settings.TextSize=(int)value;});
   body.Children.Add(Ui.Field("字重 / 加粗",Ui.Select(new[]{"常规","中等","半粗","加粗"},new[]{"normal","medium","semibold","bold"},data.Settings.TextWeight,delegate(string value){data.Settings.TextWeight=value;ApplyAppearance();ScheduleSave();})));
   AddTextSlider("任务标题字间距",0,3,data.Settings.TextSpacing,.5,"",delegate(double value){data.Settings.TextSpacing=value;});
   AddTextSlider("任务信息行距",0,16,data.Settings.TextLineGap,1,"",delegate(double value){data.Settings.TextLineGap=(int)value;});
   AddTextSlider("辅助文字浓度",50,100,data.Settings.TextContrast,1,"%",delegate(double value){data.Settings.TextContrast=(int)value;});
   body.Children.Add(Ui.Field("文字显示方式",Ui.Select(new[]{"清晰 · 适合屏幕小字","柔和 · 平滑轮廓","跟随系统","像素 · 无抗锯齿"},new[]{"sharp","soft","auto","pixel"},data.Settings.TextRendering,delegate(string value){data.Settings.TextRendering=value;ApplyAppearance();ScheduleSave();})));
   TextBlock note=Ui.Text("字号按比例调整整个界面；字间距用于任务标题。像素模式适合搭配像素字体，透明窗口下系统模式可能与清晰模式相近。",11,true);note.Margin=new Thickness(0,0,0,8);body.Children.Add(note);
   WrapPanel actions=new WrapPanel{Margin=new Thickness(-6,0,0,12)};actions.Children.Add(Ui.Button("清晰推荐",delegate{TextAppearance.Recommended(data.Settings);ApplyAppearance();SaveNow();Render();}));actions.Children.Add(Ui.Button("恢复默认字体设置",delegate{TextAppearance.Reset(data.Settings);ApplyAppearance();SaveNow();Render();}));body.Children.Add(actions);body.Children.Add(Ui.Divider());
  }
  void AddTextSlider(string title,double min,double max,double value,double step,string suffix,Action<double> changed){
   TextBlock label=Ui.Text(title+"  "+value.ToString("0.#")+suffix,12,true);label.Margin=new Thickness(0,0,0,7);body.Children.Add(label);Slider slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=step,SmallChange=step,LargeChange=step*2,IsSnapToTickEnabled=true,Margin=new Thickness(0,0,0,17)};AutomationProperties.SetName(slider,title);slider.ValueChanged+=delegate{label.Text=title+"  "+slider.Value.ToString("0.#")+suffix;changed(slider.Value);ApplyAppearance();ScheduleSave();};body.Children.Add(slider);
  }
  void ImportBackground(){
   Microsoft.Win32.OpenFileDialog dialog=new Microsoft.Win32.OpenFileDialog{Title="选择背景图片",Filter="图片文件|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*",CheckFileExists=true};
   if(dialog.ShowDialog(this)!=true)return;
   try{string encoded=BackgroundPicture.Import(dialog.FileName);data.Settings.BackgroundImage=encoded;data.Settings.BackgroundImageName=Path.GetFileName(dialog.FileName);ApplyAppearance();SaveNow();Render();}
   catch(Exception e){MessageBox.Show(this,"无法导入这张图片，请选择有效的 JPG、PNG 或 BMP 图片。\n"+e.Message,"背景图片",MessageBoxButton.OK,MessageBoxImage.Information);}
  }
  void RenderTags(){
   TextBlock fixedTags=Ui.Text("固定展示：所有任务、今日任务、已完成",11,true);fixedTags.Margin=new Thickness(3,0,0,11);body.Children.Add(fixedTags);
   WrapPanel optional=new WrapPanel{Margin=new Thickness(3,0,0,13)};string[] quickNames={"紧急","非常紧急","重要","非常重要"},quickKeys={"urgent","very-urgent","important","very-important"};for(int i=0;i<quickNames.Length;i++){string key=quickKeys[i];CheckBox choice=new CheckBox{Content="展示"+quickNames[i],IsChecked=data.Settings.QuickViews.Contains(key)};AutomationProperties.SetName(choice,"在顶栏展示："+quickNames[i]);choice.Checked+=delegate{if(!data.Settings.QuickViews.Contains(key))data.Settings.QuickViews.Add(key);ScheduleSave();};choice.Unchecked+=delegate{data.Settings.QuickViews.Remove(key);ScheduleSave();};optional.Children.Add(choice);}body.Children.Add(optional);
   TextBox input=new TextBox{MaxLength=16,MinHeight=34};AutomationProperties.SetName(input,"标签名称");Grid add=new Grid{Margin=new Thickness(0,0,0,19)};add.ColumnDefinitions.Add(new ColumnDefinition());add.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});add.Children.Add(input);
   Action addTag=delegate{if(Logic.AddTheme(data,input.Text)){SaveNow();Render();}else Toast("请输入一个尚未存在的标签，最多 16 字",null);};
   Button addButton=Ui.Button("添加标签",addTag);Grid.SetColumn(addButton,1);add.Children.Add(addButton);body.Children.Add(add);input.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter){addTag();e.Handled=true;}};
   TextBlock label=Ui.Text("标签                         顶栏展示",11,true);label.Margin=new Thickness(3,0,0,9);body.Children.Add(label);
   foreach(string tag in data.Themes.ToList()){
    string captured=tag;Grid row=new Grid{Margin=new Thickness(0,0,0,10),MinHeight=34};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});TextBlock name=Ui.Text(tag,13,false);name.VerticalAlignment=VerticalAlignment.Center;name.Margin=new Thickness(3,0,5,0);row.Children.Add(name);
    CheckBox shown=new CheckBox{Content="展示",IsChecked=Logic.ThemeVisible(data,tag),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,10,0)};AutomationProperties.SetName(shown,"在顶栏展示："+tag);shown.Checked+=delegate{Logic.SetThemeVisible(data,captured,true);ScheduleSave();};shown.Unchecked+=delegate{Logic.SetThemeVisible(data,captured,false);ScheduleSave();};Grid.SetColumn(shown,1);row.Children.Add(shown);
    Button delete=Ui.Icon("\uE74D","删除标签："+tag,delegate{
     int index=data.Themes.IndexOf(captured);bool wasVisible=Logic.ThemeVisible(data,captured);string oldCount=data.Settings.Count;List<string> tagged=data.Tasks.Where(t=>t.Tags.Contains(captured)).Select(t=>t.Id).ToList();Logic.RemoveTheme(data,captured);filterTags.Remove(captured);if(view=="tag:"+captured)view="all";SaveNow();Render();
     Toast("标签已删除，任务仍保留",delegate{if(!data.Themes.Contains(captured))data.Themes.Insert(Math.Min(index,data.Themes.Count),captured);Logic.SetThemeVisible(data,captured,wasVisible);foreach(Todo t in data.Tasks.Where(t=>tagged.Contains(t.Id)))if(!t.Tags.Contains(captured))t.Tags.Add(captured);if(oldCount=="tag:"+captured&&data.Settings.Count=="all")data.Settings.Count=oldCount;SaveNow();Render();});
    });Grid.SetColumn(delete,2);row.Children.Add(delete);body.Children.Add(row);
   }
   TextBlock note=Ui.Text("关闭展示不会删除标签。删除标签只移除分类，任务仍会保留。",11,true);note.Margin=new Thickness(3,12,3,0);body.Children.Add(note);
   Button done=Ui.Button("完成",Main);done.HorizontalAlignment=HorizontalAlignment.Right;foot.Children.Add(done);
  }
  void AddPalette(string label,bool text){
   TextBlock h=Ui.Text(label,11.5,true);h.Margin=new Thickness(0,0,0,7);body.Children.Add(h);WrapPanel palette=new WrapPanel{Margin=new Thickness(0,0,0,7)};
   for(int i=0;i<colors.Length;i++){string color=colors[i];Button swatch=Ui.Button("",delegate{if(text)data.Settings.Foreground=color;else data.Settings.Tint=color;ApplyAppearance();ScheduleSave();Render();});swatch.Width=27;swatch.Height=27;swatch.Padding=new Thickness(4);swatch.Margin=new Thickness(0,0,7,5);swatch.ToolTip=colorNames[i]+"  "+color;AutomationProperties.SetName(swatch,label+"："+colorNames[i]);System.Windows.Shapes.Ellipse circle=new System.Windows.Shapes.Ellipse{Width=18,Height=18,Fill=Ui.Brush(color),Stroke=Ui.Brush("#6CFFFFFF"),StrokeThickness=1};swatch.Content=circle;if((text?data.Settings.Foreground:data.Settings.Tint).ToUpperInvariant()==color)swatch.BorderThickness=new Thickness(1);swatch.BorderBrush=Ui.Brush("#A87F91A3");palette.Children.Add(swatch);}
   body.Children.Add(palette);StackPanel tools=new StackPanel{Orientation=Orientation.Horizontal};
   if(text){Button auto=Ui.Button("自动",delegate{data.Settings.Foreground="auto";ApplyAppearance();ScheduleSave();Render();});tools.Children.Add(auto);}
   string current=text?data.Settings.Foreground:data.Settings.Tint;Button custom=Ui.Button("调色盘…",delegate{using(Forms.ColorDialog dialog=new Forms.ColorDialog{FullOpen=true}){string initial=text?data.Settings.Foreground:data.Settings.Tint;if(initial!="auto")dialog.Color=System.Drawing.ColorTranslator.FromHtml(initial);if(dialog.ShowDialog()==Forms.DialogResult.OK){string chosen=System.Drawing.ColorTranslator.ToHtml(dialog.Color);if(chosen[0]!='#')chosen=String.Format("#{0:X2}{1:X2}{2:X2}",dialog.Color.R,dialog.Color.G,dialog.Color.B);if(text)data.Settings.Foreground=chosen;else data.Settings.Tint=chosen;ApplyAppearance();SaveNow();Render();}}});tools.Children.Add(custom);TextBlock value=Ui.Text(current=="auto"?"自动":current,10.5,true);value.VerticalAlignment=VerticalAlignment.Center;value.Margin=new Thickness(8,0,0,0);tools.Children.Add(value);tools.Margin=new Thickness(0,0,0,15);body.Children.Add(tools);
  }
  void OpenMenu(){
   ContextMenu menu=new ContextMenu();menu.FontFamily=FontFamily;TextAppearance.BindSize(menu,12);AddMenu(menu,"新建任务    Ctrl+N",NewTask);AddMenu(menu,"外观设置",Appearance);AddMenu(menu,"功能设置",Settings);AddMenu(menu,"标签管理",Tags);menu.Items.Add(new Separator());AddMenu(menu,"保持置顶",delegate{data.Settings.Mode="top";ApplyMode();ScheduleSave();});AddMenu(menu,"普通窗口",delegate{data.Settings.Mode="normal";ApplyMode();ScheduleSave();});AddMenu(menu,"保持置底",delegate{data.Settings.Mode="bottom";ApplyMode();ScheduleSave();});menu.Items.Add(new Separator());AddMenu(menu,"收起",Collapse);AddMenu(menu,"退出",Quit);menu.PlacementTarget=this;menu.Placement=PlacementMode.Relative;menu.HorizontalOffset=Math.Max(0,ActualWidth-190);menu.VerticalOffset=38;menu.IsOpen=true;
  }
  void AddMenu(ContextMenu m,string label,Action action){MenuItem item=new MenuItem{Header=label};item.Click+=delegate{action();};m.Items.Add(item);}
  void Export(){Microsoft.Win32.SaveFileDialog dialog=new Microsoft.Win32.SaveFileDialog{Filter="待办备份 (*.json)|*.json",FileName="待办备份-"+DateTime.Now.ToString("yyyyMMdd")+".json"};if(dialog.ShowDialog(this)==true){try{store.Export(data,dialog.FileName);Toast("备份已导出",null);}catch(Exception e){Toast("导出失败："+e.Message,null);}}}
  void Import(){Microsoft.Win32.OpenFileDialog dialog=new Microsoft.Win32.OpenFileDialog{Filter="待办备份 (*.json)|*.json"};if(dialog.ShowDialog(this)!=true)return;try{AppData incoming=store.Decode(File.ReadAllText(dialog.FileName));int added=0;Snapshot();foreach(Todo t in incoming.Tasks)if(!data.Tasks.Any(x=>x.Id==t.Id)){data.Tasks.Add(t);added++;}foreach(string tag in incoming.Themes)if(!data.Themes.Contains(tag))data.Themes.Add(tag);SaveNow();page="main";Render();Toast("已合并 "+added+" 项任务",Undo);}catch(Exception e){MessageBox.Show(this,"导入失败，现有任务保持不变。\n"+e.Message,"导入备份");}}
  void CheckReminders(){List<Todo> due=data.Tasks.Where(t=>!t.Done&&!t.ReminderFired&&t.Reminder!=""&&Logic.IsStamp(t.Reminder)&&Logic.Stamp(t.Reminder)<=DateTime.Now).ToList();if(due.Count==0)return;foreach(Todo t in due)t.ReminderFired=true;SaveNow();Todo first=due[0];pendingReminderId=first.Id;tray.BalloonTipTitle="待办提醒";tray.BalloonTipText=first.Title+(due.Count>1?"，另有 "+(due.Count-1)+" 项提醒":"");tray.ShowBalloonTip(10000);new ReminderWindow(this,due).Show();}
  internal int DotCount(){if(data.Settings.Count=="none")return -1;return data.Settings.Count=="current"?data.Tasks.Count(t=>!t.Done&&Matches(t)):data.Tasks.Count(t=>Logic.View(t,data.Settings.Count));}
  internal Preferences Pref {get{return data.Settings;}}
  internal void DotMoved(double left,double top){data.Settings.DotLeft=left;data.Settings.DotTop=top;ScheduleSave();}
  internal void SetDotCount(string count){data.Settings.Count=count;ScheduleSave();}
  internal void OpenFromReminder(Todo t){Restore();EditTask(t);}
  internal void Snooze(IEnumerable<Todo> items){foreach(Todo t in items){t.Reminder=DateTime.Now.AddMinutes(10).ToString("yyyy-MM-dd'T'HH:mm");t.ReminderFired=false;}SaveNow();}
 }
}
