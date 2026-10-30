using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace FrostTodo {
 public static partial class SelfTest {
  public static int Run(string[] args){
   List<string> checks=new List<string>();string report=null,testDir=null;for(int i=0;i<args.Length-1;i++){if(args[i]=="--test-report")report=args[i+1];if(args[i]=="--test-dir")testDir=args[i+1];}
   try{
    Todo month=new Todo{Title="月度",Due="2026-01-31",Repeat="monthly",Anchor=31};Assert(Logic.Next(month,new DateTime(2026,1,31))=="2026-02-28","month-end clamp",checks);month.Due="2026-02-28";Assert(Logic.Next(month,new DateTime(2026,2,28))=="2026-03-31","month-end original anchor",checks);
    Todo daily=new Todo{Title="逾期",Due="2026-01-01",Repeat="daily"};Assert(Logic.Next(daily,new DateTime(2026,10,29))=="2026-10-30","overdue repeats skip missed occurrences",checks);
    Todo week=new Todo{Title="工作日",Due="2026-10-30",Repeat="weekdays"};Assert(Logic.Next(week,new DateTime(2026,10,30))=="2026-11-02","skip weekend",checks);
    Todo custom=new Todo{Title="周一周五",Due="2026-10-30",Repeat="custom",WeekDays=new List<int>{1,5}};Assert(Logic.Next(custom,new DateTime(2026,10,30))=="2026-11-02","custom weekday",checks);
    Todo remind=new Todo{Title="重复提醒",Due="2026-10-29",Repeat="weekly",Reminder="2026-10-28T18:30",ReminderFired=true};Logic.Complete(remind,new DateTime(2026,10,29));Assert(remind.Due=="2026-11-05"&&remind.Reminder=="2026-11-04T18:30"&&!remind.ReminderFired&&!remind.Done,"completion advances reminder offset",checks);
    Todo normal=new Todo{Title="普通",Tags=new List<string>{"工作"},Urgency=2,Importance=1};Logic.Complete(normal,DateTime.Today);Assert(normal.Done&&!Logic.View(normal,"all")&&Logic.View(normal,"done"),"completed excluded from pending",checks);Logic.Complete(normal,DateTime.Today);Assert(!normal.Done&&Logic.View(normal,"tag:工作")&&Logic.View(normal,"urgent")&&Logic.View(normal,"important"),"restore and independent views",checks);
    Assert(!Logic.IsTime("25:00")&&Logic.IsTime("18:30")&&!Logic.IsDate("2026-02-30"),"date validation",checks);
    Todo high=new Todo{Title="最高级别",Urgency=2,Importance=2},medium=new Todo{Title="一般级别",Urgency=1,Importance=1};Assert(Logic.View(high,"very-urgent")&&!Logic.View(medium,"very-urgent"),"very urgent quick filter matches only level 2",checks);Assert(Logic.View(high,"very-important")&&!Logic.View(medium,"very-important"),"very important quick filter matches only level 2",checks);high.Done=true;Assert(!Logic.View(high,"very-urgent")&&!Logic.View(high,"very-important"),"highest priority quick filters exclude completed tasks",checks);
    Assert(Logic.NormalizeTime("9:5")=="09:05"&&Logic.NormalizeTime("9：30")=="09:30"&&Logic.NormalizeTime("0930")=="09:30","time input normalization",checks);
    Assert(!Logic.IsTime(Logic.NormalizeTime("25:90"))&&Logic.NormalizeTime(" ")=="","invalid or cleared optional time",checks);
    AppData tagged=new AppData();Todo both=new Todo{Title="保留任务",Tags=new List<string>{"工作","生活"}};tagged.Tasks.Add(both);Logic.SetThemeVisible(tagged,"工作",false);Assert(!Logic.ThemeVisible(tagged,"工作")&&Logic.ThemeVisible(tagged,"生活"),"hiding one tag preserves other quick tags",checks);
    Assert(Logic.AddTheme(tagged,"  新主题  ")&&Logic.ThemeVisible(tagged,"新主题")&&!Logic.AddTheme(tagged,"新主题"),"add tag and reject duplicate",checks);
    foreach(string tag in tagged.Themes.ToList())Logic.SetThemeVisible(tagged,tag,false);Assert(!tagged.Themes.Any(t=>Logic.ThemeVisible(tagged,t)),"all custom quick tags may be hidden",checks);
    tagged.Settings.Count="tag:工作";Logic.RemoveTheme(tagged,"工作");Assert(tagged.Tasks.Count==1&&both.Tags.SequenceEqual(new[]{"生活"})&&!tagged.Themes.Contains("工作")&&tagged.Settings.Count=="all","delete tag retains task and removes dangling counter",checks);
    ClearableDatePicker clearDate=new ClearableDatePicker{SelectedDate=DateTime.Today};clearDate.ClearDate();Assert(!clearDate.SelectedDate.HasValue&&clearDate.Text=="","clear date removes selected value and text",checks);
    CheckSurfaceRaster(checks);
    foreach(string fontKey in new[]{"yozai","marker","cubic"}){System.Windows.Media.FontFamily family=FontCatalog.Resolve(fontKey);System.Windows.Media.GlyphTypeface glyph;bool usable=new System.Windows.Media.Typeface(family,System.Windows.FontStyles.Normal,System.Windows.FontWeights.Normal,System.Windows.FontStretches.Normal).TryGetGlyphTypeface(out glyph);Assert(!Object.ReferenceEquals(family,FontCatalog.SystemFont)&&usable&&glyph.CharacterToGlyphMap.ContainsKey('待'),"bundled font loads: "+fontKey,checks);}
    if(testDir==null)throw new InvalidOperationException("--test-dir is required");string path=Path.Combine(Path.GetFullPath(testDir),"run-"+Guid.NewGuid().ToString("N"));Store store=new Store(path);AppData d=new AppData();d.Tasks.Add(normal);d.Settings.Tint="#7B8AD7";d.Settings.Foreground="#FFC44F";store.Save(d);d.Tasks[0].Title="中文任务：持久保存";store.Save(d);AppData loaded=store.Load();Assert(loaded.Tasks[0].Title=="中文任务：持久保存"&&loaded.Settings.Foreground=="#FFC44F","unicode and independent colors survive restart",checks);
    AppData backup=store.Decode(store.Encode(d));Assert(backup.Tasks.Count==1&&backup.Tasks[0].Tags[0]=="工作","backup round trip",checks);
    d.Settings.QuickViews.Clear();d.Settings.Font="cubic";AppData selected=store.Decode(store.Encode(d));Assert(selected.Settings.QuickViews.Count==0&&selected.Settings.Font=="cubic","optional built-in views and chosen font persist",checks);
    d.Settings.QuickViews=new List<string>{"very-urgent","very-important"};AppData priorityViews=store.Decode(store.Encode(d));Assert(priorityViews.Settings.QuickViews.SequenceEqual(d.Settings.QuickViews),"highest priority display switches persist independently",checks);
    AppData retiredRadius=store.Decode("{\"Version\":1,\"Tasks\":[],\"Settings\":{\"Tint\":\"#D8E6F2\",\"Radius\":60}}");Assert(retiredRadius.Settings.Tint==(UiVariant.IsGpt?"#D8E6F2":"#F3F6FA")&&!store.Encode(retiredRadius).Contains("Radius"),"old settings migrate to white and ignore retired radius",checks);
    retiredRadius.Settings.Tint="#465C47";Assert(store.Decode(store.Encode(retiredRadius)).Settings.Tint=="#465C47","later chosen colors survive white migration",checks);
    System.Windows.Media.Color light=System.Windows.Media.Color.FromRgb(243,246,250);uint packed=Native.PackTint(light,55);Assert((packed&0xFFFFFF)==0xFAF6F3&&(packed>>24)==140,"native material receives light tint and selected opacity",checks);
    System.Windows.Controls.Border nativeBorder=new System.Windows.Controls.Border();Ui.PaintWindowSurface(nativeBorder,light,55,true);Assert(((System.Windows.Media.SolidColorBrush)nativeBorder.Background).Color.A==1&&((System.Windows.Media.SolidColorBrush)nativeBorder.BorderBrush).Color.A==140,"native material keeps minimally visible mouse-hit surface and selected border opacity",checks);
    CheckNativeMouseSurface(checks);
    bool rejected=false;try{store.Decode("{\"Version\":1,\"Tasks\":[{\"Title\":\"bad\",\"Id\":\"x\",\"Due\":\"not a date\"}]}");}catch(Exception){rejected=true;}Assert(rejected,"invalid backup rejected before merge",checks);
    File.WriteAllText(store.FilePath,"broken JSON");AppData restored=store.Load();Assert(restored.Tasks[0].Title=="普通"&&Directory.GetFiles(path,"*.damaged-*").Length==1,"corrupt primary recovered and preserved",checks);
    CheckBackgroundPicture(path,checks);
    CheckTextAppearance(path,checks);
    CheckTaskOrdering(path,checks);
    CheckRecurrence(path,checks);
    CheckGptTheme(path,checks);
    Assert(AppIcon.WindowIcon.PixelWidth==256&&AppIcon.WindowIcon.PixelHeight==256&&AppIcon.WindowIcon.IsFrozen,"embedded leaf window icon loads independently of asset files",checks);
    using(System.Drawing.Icon leaf=AppIcon.CreateTrayIcon())using(System.Drawing.Bitmap bitmap=leaf.ToBitmap()){bool painted=false;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)painted=painted||bitmap.GetPixel(x,y).A>0;Assert(leaf.Width==32&&leaf.Height==32&&bitmap.GetPixel(0,0).A==0&&painted,"embedded leaf tray icon is painted and retains transparent corners "+leaf.Width+"x"+leaf.Height+" alpha="+bitmap.GetPixel(0,0).A+" painted="+painted,checks);}
    Write(report,"PASS "+checks.Count+Environment.NewLine+String.Join(Environment.NewLine,checks));return 0;
   }catch(Exception e){Write(report,"FAIL\n"+String.Join(Environment.NewLine,checks)+"\n"+e);return 1;}
  }
  static void Assert(bool condition,string name,List<string> checks){if(!condition)throw new Exception(name);checks.Add("PASS "+name);}
  static void CheckTaskOrdering(string path,List<string> checks){
   Func<string,Todo> task=id=>new Todo{Id=id,Title="任务 "+id,Tags=new List<string>{"工作"}};
   List<Todo> tasks=new[]{"a","b","c","d"}.Select(task).ToList();
   Assert(TaskOrdering.Move(tasks,tasks.Select(t=>t.Id),"d","a",false)&&String.Join("",tasks.Select(t=>t.Id))=="dabc","dragging last task before first preserves all task data",checks);
   Assert(TaskOrdering.Move(tasks,tasks.Select(t=>t.Id),"d","c",true)&&String.Join("",tasks.Select(t=>t.Id))=="abcd","dragging first task below last inserts after target",checks);
   Assert(TaskOrdering.Move(tasks,tasks.Select(t=>t.Id),"a","c",false)&&String.Join("",tasks.Select(t=>t.Id))=="bacd","downward insertion accounts for removing the source row",checks);
   Assert(!TaskOrdering.Move(tasks,tasks.Select(t=>t.Id),"a","a",true)&&!TaskOrdering.Move(tasks,tasks.Select(t=>t.Id),"a","c",false)&&String.Join("",tasks.Select(t=>t.Id))=="bacd","dropping on self or unchanged adjacent position does not mutate order",checks);
   Todo hidden=task("hidden"),done=task("done");hidden.Tags=new List<string>{"生活"};done.Done=true;tasks=new List<Todo>{task("a"),hidden,task("b"),done,task("c")};
   Assert(TaskOrdering.Move(tasks,tasks.Where(t=>!t.Done&&t.Tags.Contains("工作")).Select(t=>t.Id),"c","a",false)&&String.Join(",",tasks.Select(t=>t.Id))=="c,hidden,a,done,b"&&Object.ReferenceEquals(tasks[1],hidden)&&Object.ReferenceEquals(tasks[3],done),"filtered sorting changes only visible slots and leaves hidden and completed tasks in place",checks);
   string original=String.Join(",",tasks.Select(t=>t.Id));
   Assert(!TaskOrdering.Move(tasks,new[]{"a","b","c"},"hidden","a",false)&&!TaskOrdering.Move(tasks,new[]{"a","b","c"},"a","missing",true)&&String.Join(",",tasks.Select(t=>t.Id))==original,"stale or hidden drag targets cannot change the task list",checks);
   Assert(TaskOrdering.Move(tasks,new[]{"a","a","b","c","missing"},"c","b",true)&&String.Join(",",tasks.Select(t=>t.Id))=="a,hidden,b,done,c","duplicate or stale visible ids cannot duplicate or lose tasks",checks);
   List<Todo> undo=tasks.Select(t=>t.Copy()).ToList();TaskOrdering.Move(tasks,new[]{"a","b","c"},"a","c",true);List<Todo> reordered=tasks.ToList();tasks=undo;
   Assert(String.Join(",",tasks.Select(t=>t.Id))=="a,hidden,b,done,c"&&String.Join(",",reordered.Select(t=>t.Id))=="b,hidden,c,done,a","order snapshots restore the previous manual sequence",checks);
   AppData data=new AppData{Tasks=reordered};Store store=new Store(Path.Combine(path,"ordering"));store.Save(data);AppData restored=store.Load();Assert(restored.Tasks.Select(t=>t.Id).SequenceEqual(reordered.Select(t=>t.Id))&&restored.Tasks.Count==5&&restored.Tasks.First(t=>t.Id=="hidden").Tags.SequenceEqual(new[]{"生活"}),"manual order survives restart without changing tags or task ids",checks);
   Assert(store.Decode(store.Encode(restored)).Tasks.Select(t=>t.Id).SequenceEqual(reordered.Select(t=>t.Id)),"backup round trip preserves manual order",checks);
   string savedOrder=String.Join(",",restored.Tasks.Select(t=>t.Id));Logic.Complete(restored.Tasks[0],DateTime.Today);Logic.Complete(restored.Tasks[0],DateTime.Today);Assert(String.Join(",",restored.Tasks.Select(t=>t.Id))==savedOrder,"complete and restore retain the manual sequence",checks);
   tasks=new List<Todo>{task("only")};Assert(!TaskOrdering.Move(tasks,new[]{"only"},"only","only",false)&&tasks.Count==1,"single task sorting is a no-op",checks);
   TaskHoldGesture gesture=new TaskHoldGesture();gesture.Press(10,10,1000);Assert(!gesture.Activate(1449,true)&&gesture.Pending&&!gesture.Release(),"short press retains ordinary edit click behavior",checks);
   gesture.Press(10,10,2000);Assert(gesture.Activate(2450,true)&&gesture.Active&&gesture.Release(),"long press starts sorting and consumes the edit click",checks);
   gesture.Press(10,10,3000);gesture.Move(19,10);Assert(!gesture.Activate(4000,true)&&gesture.CancelledByMovement&&gesture.Release(),"movement before hold threshold cancels sorting and accidental edit",checks);
   gesture.Press(10,10,4000);gesture.Move(14,13);Assert(gesture.Activate(4450,true),"small pointer jitter does not cancel long press",checks);gesture.Move(100,300);Assert(gesture.Active&&gesture.Release(),"large movement after long press remains a sortable drag",checks);
   gesture.Press(0,0,5000);Assert(!gesture.Activate(5500,false)&&!gesture.Active&&!gesture.Release(),"release outside timer cannot leave a stuck sorting gesture",checks);
   gesture.Press(0,0,6000);gesture.Activate(6500,true);gesture.Reset();gesture.Press(0,0,7000);Assert(!gesture.Activate(7100,true)&&!gesture.Active&&gesture.Pending,"cancel or escape clears the previous hold timer",checks);gesture.Reset();
  }
  static void CheckTextAppearance(string path,List<string> checks){
   Store store=new Store(Path.Combine(path,"text-settings"));AppData data=new AppData();data.Settings.TextSize=19;data.Settings.TextWeight="bold";data.Settings.TextRendering="pixel";data.Settings.TextContrast=98;data.Settings.TextSpacing=2.5;data.Settings.TextLineGap=12;data.Settings.Font="yahei";store.Save(data);Preferences loaded=store.Load().Settings;
   Assert(loaded.TextSize==19&&loaded.TextWeight=="bold"&&loaded.TextRendering=="pixel"&&loaded.TextContrast==98&&loaded.TextSpacing==2.5&&loaded.TextLineGap==12&&loaded.Font=="yahei","all font controls persist across restart",checks);
   Preferences legacy=store.Decode("{\"Version\":1,\"Tasks\":[],\"Settings\":{\"Font\":\"marker\"}}").Settings;Assert(legacy.TextSize==15&&legacy.TextWeight=="normal"&&legacy.TextRendering=="sharp"&&legacy.TextContrast==(UiVariant.IsGpt?100:76)&&legacy.Font=="marker","old preferences gain font control defaults without changing chosen font",checks);
   Preferences bad=new Preferences{TextSize=100,TextWeight="bad",TextRendering="bad",TextContrast=-8,TextSpacing=Double.NaN,TextLineGap=99};TextAppearance.Normalize(bad);Assert(bad.TextSize==22&&bad.TextWeight=="normal"&&bad.TextRendering=="sharp"&&bad.TextContrast==50&&bad.TextSpacing==0&&bad.TextLineGap==16,"invalid font controls are normalized",checks);
   System.Windows.Application app=new System.Windows.Application();Ui.Install(app);Preferences p=new Preferences();Ui.SetFont("yahei");TextAppearance.Publish(p);
   System.Windows.Controls.StackPanel host=new System.Windows.Controls.StackPanel{Resources=app.Resources};TextAppearance.Configure(host);SpacedTitle title=TextAppearance.Title("待办 ABC 123");System.Windows.Controls.TextBlock label=Ui.Text("辅助说明",12,true);System.Windows.Controls.Button button=Ui.Pill("今日任务",false,delegate{});System.Windows.Controls.TextBox input=new System.Windows.Controls.TextBox();ClearableDatePicker date=new ClearableDatePicker{SelectedDate=DateTime.Today};TimePicker time=new TimePicker("时间","09:30");host.Children.Add(title);host.Children.Add(label);host.Children.Add(button);host.Children.Add(input);host.Children.Add(date);host.Children.Add(time);
   host.Measure(new System.Windows.Size(300,1000));host.Arrange(new System.Windows.Rect(0,0,300,1000));host.UpdateLayout();double originalWidth=title.DesiredSize.Width;
   Assert(title.FontSize==15&&label.FontSize==12&&button.FontSize==(UiVariant.IsGpt?12.5:11.5)&&input.FontSize==(UiVariant.IsGpt?14:13),"font scale applies to task, labels, quick tabs and editable controls",checks);
   p.TextSize=22;p.TextWeight="bold";TextAppearance.Publish(p);host.Measure(new System.Windows.Size(300,1000));host.Arrange(new System.Windows.Rect(0,0,300,1000));host.UpdateLayout();
   Assert(title.FontSize==22&&label.FontSize>17&&button.FontSize>16&&input.FontSize>19&&title.FontWeight==System.Windows.FontWeights.Bold&&input.FontWeight==System.Windows.FontWeights.Bold,"font size and bold update existing controls immediately",checks);
   Assert(title.DesiredSize.Width>originalWidth&&button.DesiredSize.Height<=(double)app.Resources["UiTabsHeight"],"larger font measures correctly and quick tab height accommodates text height="+button.DesiredSize.Height+" limit="+app.Resources["UiTabsHeight"]+" width="+title.DesiredSize.Width+" original="+originalWidth,checks);
   Assert(date.ActualHeight==time.ActualHeight&&date.ActualHeight==Ui.InputHeight,"font changes retain matching date and time control heights",checks);
   foreach(string mode in new[]{"sharp","soft","auto","pixel"}){p.TextRendering=mode;TextAppearance.Publish(p);System.Windows.Media.TextFormattingMode format=System.Windows.Media.TextOptions.GetTextFormattingMode(title);System.Windows.Media.TextRenderingMode rendering=System.Windows.Media.TextOptions.GetTextRenderingMode(title);Assert(format==(mode=="soft"?System.Windows.Media.TextFormattingMode.Ideal:System.Windows.Media.TextFormattingMode.Display)&&rendering==(mode=="auto"?System.Windows.Media.TextRenderingMode.Auto:mode=="pixel"?System.Windows.Media.TextRenderingMode.Aliased:System.Windows.Media.TextRenderingMode.Grayscale),"live text rendering option "+mode,checks);}
   p.TextSize=15;p.TextSpacing=0;TextAppearance.Publish(p);title.Measure(new System.Windows.Size(Double.PositiveInfinity,100));double unspaced=title.DesiredSize.Width;p.TextSpacing=3;TextAppearance.Publish(p);title.Measure(new System.Windows.Size(Double.PositiveInfinity,100));Assert(title.DesiredSize.Width>unspaced+15,"letter spacing increases measured heading width",checks);
   foreach(double tracking in new[]{0.0,3.0})foreach(int dpi in new[]{96,120,144,192}){
    p.TextSpacing=tracking;p.TextRendering=tracking==0?"sharp":"pixel";TextAppearance.Publish(p);title.HorizontalAlignment=System.Windows.HorizontalAlignment.Left;title.MaxWidth=120;title.Text="很长的任务标题 ABC 123 👋 需要省略显示";title.TextDecorations=null;title.Measure(new System.Windows.Size(120,100));title.Arrange(new System.Windows.Rect(0,0,120,title.DesiredSize.Height));title.UpdateLayout();int width=(int)Math.Ceiling(140*dpi/96.0),height=(int)Math.Ceiling(60*dpi/96.0);System.Windows.Media.Imaging.RenderTargetBitmap raster=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,dpi,dpi,System.Windows.Media.PixelFormats.Pbgra32);raster.Render(title);byte[] pixels=new byte[width*height*4];raster.CopyPixels(pixels,width*4,0);bool painted=false,leaked=false;for(int y=0;y<height;y++)for(int x=0;x<width;x++){byte alpha=pixels[(y*width+x)*4+3];if(alpha>0)painted=true;if(x>Math.Ceiling(120*dpi/96.0)+1&&alpha>0)leaked=true;}
    Assert(painted&&!leaked&&title.DesiredSize.Width<=120,"task text renders and truncates at DPI "+dpi+" and spacing "+tracking,checks);
   }
   System.Windows.Automation.Peers.AutomationPeer peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(title);Assert(peer.GetName()==title.Text&&peer.GetAutomationControlType()==System.Windows.Automation.Peers.AutomationControlType.Text,"spaced title retains accessible full task text",checks);
   string picture=data.Settings.BackgroundImage="preserved";string tint=data.Settings.Tint="#465C47";TextAppearance.Recommended(data.Settings);Assert(data.Settings.Font=="yahei"&&data.Settings.TextSize==16&&data.Settings.TextContrast==100&&data.Settings.BackgroundImage==picture&&data.Settings.Tint==tint,"clear reading preset preserves background appearance",checks);TextAppearance.Reset(data.Settings);Assert(data.Settings.TextSize==15&&data.Settings.TextSpacing==0&&data.Settings.TextWeight=="normal"&&data.Settings.BackgroundImage==picture,"font reset affects typography only",checks);
  }
  static void CheckBackgroundPicture(string path,List<string> checks){
   int w=2000,h=1000;byte[] input=new byte[w*h*4];for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=(y*w+x)*4;byte value=(byte)(x<w/2?0:255);input[i]=value;input[i+1]=value;input[i+2]=value;input[i+3]=255;}
   System.Windows.Media.Imaging.BitmapSource source=System.Windows.Media.Imaging.BitmapSource.Create(w,h,96,96,System.Windows.Media.PixelFormats.Bgra32,null,input,w*4);
   string encoded=null;foreach(string format in new[]{"png","jpg","bmp"}){
    System.Windows.Media.Imaging.BitmapEncoder encoder=format=="png"?(System.Windows.Media.Imaging.BitmapEncoder)new System.Windows.Media.Imaging.PngBitmapEncoder():format=="jpg"?(System.Windows.Media.Imaging.BitmapEncoder)new System.Windows.Media.Imaging.JpegBitmapEncoder():new System.Windows.Media.Imaging.BmpBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));string file=Path.Combine(path,"背景测试."+format);using(FileStream stream=File.Create(file))encoder.Save(stream);
    string imported=BackgroundPicture.Import(file);File.Delete(file);System.Windows.Media.Imaging.BitmapSource decoded=BackgroundPicture.Decode(imported);Assert(decoded.PixelWidth==1600&&decoded.PixelHeight==800,"background "+format+" import keeps aspect ratio, resizes and releases original file",checks);if(format=="png")encoded=imported;
   }
   Store store=new Store(Path.Combine(path,"picture-settings"));AppData data=new AppData();data.Settings.BackgroundImage=encoded;data.Settings.BackgroundImageName="背景测试.png";store.Save(data);AppData loaded=store.Load();Assert(loaded.Settings.BackgroundImage==encoded&&loaded.Settings.BackgroundImageName=="背景测试.png","background survives restart without the original image",checks);
   BackgroundPictureLayer layer=new BackgroundPictureLayer();System.Windows.Media.Color white=System.Windows.Media.Colors.White;
   byte[] fixedPicture=null;
   foreach(int opacity in new[]{10,55,95}){
    loaded.Settings.Opacity=opacity;Assert(layer.Apply(loaded.Settings,white)&&!layer.IsHitTestVisible,"background loads without intercepting window dragging at opacity "+opacity,checks);
    layer.Measure(new System.Windows.Size(400,400));layer.Arrange(new System.Windows.Rect(0,0,400,400));layer.UpdateLayout();System.Windows.Media.Imaging.RenderTargetBitmap bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(400,400,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(layer);byte[] pixels=new byte[400*400*4];bitmap.CopyPixels(pixels,400*4,0);int center=(200*400+190)*4,left=(200*400+50)*4,right=(200*400+350)*4;
    Assert(pixels[center]>pixels[left]&&pixels[center]<pixels[right],"background raster softens a sharp image boundary at opacity "+opacity,checks);
    int alpha=pixels[(200*400+200)*4+3];bool covered=alpha==255;foreach(int index in new[]{0,399,399*400,400*400-1})covered=covered&&pixels[index*4+3]==255;
    Assert(covered,"picture remains opaque at all four edges while frost opacity is "+opacity,checks);
    Assert(Math.Abs(pixels[left]-opacity*2.55)<=1,"only frost tint strength follows opacity "+opacity,checks);
    System.Windows.Media.Imaging.RenderTargetBitmap rawPicture=new System.Windows.Media.Imaging.RenderTargetBitmap(496,496,96,96,System.Windows.Media.PixelFormats.Pbgra32);rawPicture.Render(layer.Children[0]);byte[] rawPixels=new byte[496*496*4];rawPicture.CopyPixels(rawPixels,496*4,0);if(fixedPicture==null)fixedPicture=rawPixels;
    Assert(rawPixels.SequenceEqual(fixedPicture)&&layer.Opacity==1,"blurred image pixels stay unchanged when frost opacity is "+opacity,checks);
   }
   foreach(System.Windows.Size size in new[]{new System.Windows.Size(300,900),new System.Windows.Size(1000,350)}){
    layer.Measure(size);layer.Arrange(new System.Windows.Rect(size));layer.UpdateLayout();int width=(int)size.Width,height=(int)size.Height;System.Windows.Media.Imaging.RenderTargetBitmap resized=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);resized.Render(layer);byte[] pixels=new byte[width*height*4];resized.CopyPixels(pixels,width*4,0);int center=(height/2*width+width/2)*4,alpha=pixels[center+3];bool covered=true;foreach(int index in new[]{0,width-1,(height-1)*width,width*height-1})covered=covered&&Math.Abs(pixels[index*4+3]-alpha)<=1;
    int left=(height/2*width+width/8)*4,right=(height/2*width+width*7/8)*4;
    Assert(covered&&pixels[center]>pixels[left]&&pixels[center]<pixels[right],"resized picture stays centered and covers window "+width+"x"+height,checks);
   }
   loaded.Settings.BackgroundImage="invalid";Assert(!layer.Apply(loaded.Settings,white),"damaged background falls back to frosted color",checks);
   loaded.Settings.BackgroundImage=encoded;layer.Apply(loaded.Settings,white);loaded.Settings.BackgroundImage="";Assert(!layer.Apply(loaded.Settings,white)&&layer.Visibility==System.Windows.Visibility.Collapsed,"removing background restores plain frosted surface",checks);
   string bad=Path.Combine(path,"not-image.png");File.WriteAllText(bad,"invalid image");bool rejected=false;try{BackgroundPicture.Import(bad);}catch(Exception){rejected=true;}Assert(rejected,"invalid image is rejected before changing preferences",checks);
  }
  static void CheckSurfaceRaster(List<string> checks){
   foreach(int opacity in new[]{10,55,95}){
    System.Windows.Controls.Border border=new System.Windows.Controls.Border();Ui.PaintWindowSurface(border,System.Windows.Media.Color.FromRgb(216,230,242),opacity);border.Measure(new System.Windows.Size(256,256));border.Arrange(new System.Windows.Rect(0,0,256,256));border.UpdateLayout();
    System.Windows.Media.Imaging.RenderTargetBitmap bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(256,256,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(border);byte[] pixels=new byte[256*256*4];bitmap.CopyPixels(pixels,256*4,0);bool uniform=true;int sample=(128*256+128)*4;
    for(int y=3;y<253&&uniform;y++)for(int x=3;x<253&&uniform;x++)for(int c=0;c<4;c++)if(pixels[(y*256+x)*4+c]!=pixels[sample+c]){uniform=false;break;}
    Assert(uniform,"surface raster has uniform color/alpha without diagonal bands at opacity "+opacity,checks);
    byte alpha=(byte)Math.Round(opacity*2.55);bool white=true;foreach(int p in new[]{0,255,255*256,256*256-1})white=white&&pixels[p*4]==alpha&&pixels[p*4+1]==alpha&&pixels[p*4+2]==alpha&&pixels[p*4+3]==alpha;
    Assert(white,"white square frame follows background opacity "+opacity,checks);
   }
  }
  static void CheckNativeMouseSurface(List<string> checks){
   foreach(int opacity in new[]{10,55,95}){
    System.Windows.Controls.Border border=new System.Windows.Controls.Border();Ui.PaintWindowSurface(border,System.Windows.Media.Color.FromRgb(243,246,250),opacity,true);border.Measure(new System.Windows.Size(256,256));border.Arrange(new System.Windows.Rect(0,0,256,256));border.UpdateLayout();
    System.Windows.Media.Imaging.RenderTargetBitmap bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(256,256,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(border);byte[] pixels=new byte[256*256*4];bitmap.CopyPixels(pixels,256*4,0);bool hittable=true;for(int y=2;y<254&&hittable;y++)for(int x=2;x<254;x++)if(pixels[(y*256+x)*4+3]==0){hittable=false;break;}
    Assert(hittable,"native material blank area has no fully transparent mouse-pass-through pixels at opacity "+opacity,checks);
   }
  }
  static void Write(string path,string text){if(path!=null){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllText(path,text);}else Console.WriteLine(text);}
 }
}
