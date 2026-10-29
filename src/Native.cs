using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Text;

namespace FrostTodo {
 public static class Native {
  [StructLayout(LayoutKind.Sequential)]public struct Rect{public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)]struct Margins{public int Left,Right,Top,Bottom;}
  [StructLayout(LayoutKind.Sequential)]struct AccentPolicy{public int State,Flags;public uint Color;public int Animation;}
  [StructLayout(LayoutKind.Sequential)]struct CompositionData{public int Attribute;public IntPtr Data;public IntPtr Size;}
  public delegate bool EnumWindowProc(IntPtr hwnd,IntPtr param);
  [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hgt,uint flags);
  [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder name,int count);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int command);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumWindowProc callback,IntPtr param);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetLong(IntPtr h,int index);
  [DllImport("user32.dll")]public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
  [DllImport("user32.dll")]public static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")]public static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
  [DllImport("user32.dll")]public static extern bool ReleaseCapture();
  [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
  [DllImport("dwmapi.dll")]static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref Margins margins);
  [DllImport("user32.dll")]static extern bool SetWindowCompositionAttribute(IntPtr h,ref CompositionData data);
  [DllImport("user32.dll")]static extern int SetWindowRgn(IntPtr h,IntPtr region,bool redraw);
  public static void Square(Window window){
   IntPtr h=new WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return;
   int corner=1,border=unchecked((int)0xFFFFFFFE),material=1;DwmSetWindowAttribute(h,33,ref corner,4);DwmSetWindowAttribute(h,34,ref border,4);DwmSetWindowAttribute(h,38,ref material,4);SetWindowRgn(h,IntPtr.Zero,true);
  }
  public static bool Glass(Window window,Color color,int opacity){
   IntPtr h=new WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return false;
   if(!window.AllowsTransparency){HwndSource source=HwndSource.FromHwnd(h);if(source!=null)source.CompositionTarget.BackgroundColor=Colors.Transparent;Margins margins=new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1};DwmExtendFrameIntoClientArea(h,ref margins);}
   // Restore the original tinted acrylic; a transparent content brush avoids applying tint twice.
   AccentPolicy p=new AccentPolicy{State=4,Flags=0,Color=PackTint(color,opacity)};
   IntPtr memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AccentPolicy)));try{Marshal.StructureToPtr(p,memory,false);CompositionData d=new CompositionData{Attribute=19,Data=memory,Size=new IntPtr(Marshal.SizeOf(typeof(AccentPolicy)))};return SetWindowCompositionAttribute(h,ref d);}catch(EntryPointNotFoundException){return false;}finally{Marshal.FreeHGlobal(memory);}
  }
  internal static uint PackTint(Color color,int opacity){return ((uint)Math.Round(Math.Max(1,Math.Min(100,opacity))*2.55)<<24)|((uint)color.B<<16)|((uint)color.G<<8)|color.R;}
  public static void PlaceBottom(Window w){
   IntPtr own=new WindowInteropHelper(w).Handle;if(own==IntPtr.Zero)return;IntPtr desktop=IntPtr.Zero;
   EnumWindows(delegate(IntPtr h,IntPtr unused){if(FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){desktop=h;return false;}return true;},IntPtr.Zero);
   IntPtr after=IntPtr.Zero;uint ownPid;GetWindowThreadProcessId(own,out ownPid);
   EnumWindows(delegate(IntPtr h,IntPtr unused){if(h==desktop)return false;if(h==own||!IsWindowVisible(h)||(GetLong(h,-20).ToInt64()&8)!=0)return true;uint pid;GetWindowThreadProcessId(h,out pid);if(pid==ownPid)return true;StringBuilder cls=new StringBuilder(128);GetClassName(h,cls,cls.Capacity);if(cls.ToString()=="Progman"||cls.ToString()=="WorkerW")return true;after=h;return true;},IntPtr.Zero);
   SetWindowPos(own,after,0,0,0,0,0x0013);
  }
  public static void RestoreBottom(Window w){IntPtr h=new WindowInteropHelper(w).Handle;ShowWindow(h,4);PlaceBottom(w);}
  public sealed class ForegroundWatch:IDisposable {
   delegate void Callback(IntPtr hook,uint e,IntPtr hwnd,int obj,int child,uint thread,uint time);
   [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,Callback callback,uint pid,uint thread,uint flags);
   [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
   readonly Callback callback;IntPtr handle;
   public ForegroundWatch(Action changed){callback=delegate(IntPtr hook,uint e,IntPtr hwnd,int obj,int child,uint thread,uint time){changed();};handle=SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,2);}
   public void Dispose(){if(handle!=IntPtr.Zero){UnhookWinEvent(handle);handle=IntPtr.Zero;}GC.KeepAlive(callback);}
  }
  public static void Layer(Window w,string mode){IntPtr h=new WindowInteropHelper(w).Handle;SetWindowPos(h,new IntPtr(mode=="top"?-1:mode=="bottom"?1:-2),0,0,0,0,0x0013);}
  public static void Resize(Window w,int hit){ReleaseCapture();SendMessage(new WindowInteropHelper(w).Handle,0x00A1,new IntPtr(hit),IntPtr.Zero);}
 }
}
