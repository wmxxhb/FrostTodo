using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;

namespace FrostTodo {
 public static class Program {
  [STAThread] public static int Main(string[] args){
   if(args.Contains("--self-test"))return SelfTest.Run(args);
   string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),UiVariant.DataFolder);
   for(int i=0;i<args.Length-1;i++)if(args[i]=="--data-dir")dir=Path.GetFullPath(args[i+1]);
   string hash;using(SHA256 sha=SHA256.Create()){hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(dir.ToLowerInvariant()))).Replace("-","").Substring(0,16);}
   bool created;using(Mutex mutex=new Mutex(true,"Local\\FrostTodo-"+hash,out created))using(EventWaitHandle wake=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\FrostTodo-Wake-"+hash)){
    if(!created){wake.Set();return 0;}
    Application app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Ui.Install(app);
    app.DispatcherUnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e){try{Directory.CreateDirectory(dir);File.AppendAllText(Path.Combine(dir,"error.log"),DateTime.Now.ToString("o")+" "+e.Exception+Environment.NewLine);}catch(Exception){}MessageBox.Show("操作未完成，数据文件保留。\n"+e.Exception.Message,"桌面待办");e.Handled=true;};
    try{Store store=new Store(dir);AppData data=UiVariant.LoadProfile(store,args.Contains("--data-dir")?null:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FrostTodo"));MainWindow window=new MainWindow(store,data);app.MainWindow=window;
     RegisteredWaitHandle registration=ThreadPool.RegisterWaitForSingleObject(wake,delegate{app.Dispatcher.BeginInvoke(new Action(window.Restore));},null,-1,false);
     if(args.Contains("--visual-check")){Window background=new Window{Title="FrostTodo 材质对照背景",Width=620,Height=760,Left=20,Top=45,ShowInTaskbar=true};System.Windows.Controls.Grid grid=new System.Windows.Controls.Grid();string[] paint={"#B84542","#286FAD","#B99C32"};for(int i=0;i<3;i++){grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());System.Windows.Controls.Border band=new System.Windows.Controls.Border{Background=Ui.Brush(paint[i])};System.Windows.Controls.Grid.SetColumn(band,i);grid.Children.Add(band);}background.Content=grid;background.Show();}
     window.Show();if(store.RecoveryMessage!="")window.Toast(store.RecoveryMessage,null);int result=app.Run();registration.Unregister(null);return result;
    }catch(Exception e){Directory.CreateDirectory(dir);File.AppendAllText(Path.Combine(dir,"error.log"),e+Environment.NewLine);MessageBox.Show(e.Message,"桌面待办无法启动");return 1;}
   }
  }
 }
}
