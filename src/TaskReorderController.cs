using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FrostTodo {
 public sealed class TaskReorderController:IDisposable {
  sealed class Row {public string Id;public Button Handle;public Border View;}
  readonly List<Row> rows=new List<Row>();readonly TaskHoldGesture gesture=new TaskHoldGesture();
  readonly Grid root;readonly ScrollViewer scroll;readonly Action<string,string,bool> dropped;
  readonly Canvas overlay=new Canvas{IsHitTestVisible=false};readonly Border marker=new Border{Height=2,Visibility=Visibility.Collapsed};
  readonly DispatcherTimer holdTimer=new DispatcherTimer(),edgeTimer=new DispatcherTimer();Row source,target;bool after;Cursor oldCursor;bool cleaning;
  public bool IsHolding{get{return source!=null;}}
  static long Now(){return (long)(Stopwatch.GetTimestamp()*(1000.0/Stopwatch.Frequency));}
  public TaskReorderController(Grid root,ScrollViewer scroll,Action<string,string,bool> dropped){
   this.root=root;this.scroll=scroll;this.dropped=dropped;marker.SetResourceReference(Border.BackgroundProperty,"Accent");overlay.Children.Add(marker);root.Children.Add(overlay);
   holdTimer.Interval=TimeSpan.FromMilliseconds(TaskHoldGesture.DelayMilliseconds);holdTimer.Tick+=Hold;
   edgeTimer.Interval=TimeSpan.FromMilliseconds(30);edgeTimer.Tick+=ScrollEdge;
   scroll.PreviewMouseWheel+=OnWheel;
  }
  public void Bind(Button handle,Border view,string id){
   Row row=new Row{Id=id,Handle=handle,View=view};rows.Add(row);handle.ToolTip="点击编辑 · 长按拖动排序";
   handle.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){
    if(e.ClickCount!=1)return;Cancel();source=row;Point p=e.GetPosition(root);gesture.Press(p.X,p.Y,Now());holdTimer.Interval=TimeSpan.FromMilliseconds(TaskHoldGesture.DelayMilliseconds);holdTimer.Start();
   };
   handle.PreviewMouseMove+=delegate(object sender,MouseEventArgs e){
    if(source!=row)return;if(e.LeftButton!=MouseButtonState.Pressed){Cancel();return;}
    Point p=e.GetPosition(root);gesture.Move(p.X,p.Y);if(!gesture.Pending)holdTimer.Stop();if(gesture.Active){UpdateTarget();e.Handled=true;}
   };
   handle.PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){
    if(source!=row)return;bool active=gesture.Active;if(active)UpdateTarget();string sourceId=row.Id,targetId=target==null?null:target.Id;bool insertAfter=after;Point p=e.GetPosition(scroll);bool inside=p.X>=0&&p.X<=scroll.ActualWidth&&p.Y>=0&&p.Y<=scroll.ActualHeight;
    bool suppress=gesture.Release();if(suppress)e.Handled=true;Finish(suppress);
    if(active&&inside&&targetId!=null)dropped(sourceId,targetId,insertAfter);
   };
   handle.LostMouseCapture+=delegate{if(!cleaning&&source==row)Cancel();};
   handle.Unloaded+=delegate{if(source==row)Cancel();};
  }
  void Hold(object sender,EventArgs e){
   holdTimer.Stop();if(source==null)return;Point p=Mouse.GetPosition(root);gesture.Move(p.X,p.Y);
   if(!gesture.Activate(Now(),Mouse.LeftButton==MouseButtonState.Pressed)){if(gesture.Pending){holdTimer.Interval=TimeSpan.FromMilliseconds(16);holdTimer.Start();}return;}
   oldCursor=source.Handle.Cursor;source.Handle.Cursor=Cursors.SizeNS;source.View.Opacity=.55;
   if(!source.Handle.CaptureMouse()){Cancel();return;}UpdateTarget();edgeTimer.Start();
  }
  void UpdateTarget(){
   if(!gesture.Active||source==null)return;Point p=Mouse.GetPosition(scroll);target=null;
   if(p.X<0||p.X>scroll.ActualWidth||p.Y<0||p.Y>scroll.ActualHeight){marker.Visibility=Visibility.Collapsed;return;}
   foreach(Row row in rows){Point origin=row.View.TranslatePoint(new Point(),scroll);target=row;after=p.Y>=origin.Y+row.View.ActualHeight/2;if(p.Y<origin.Y+row.View.ActualHeight)break;}
   if(target==null){marker.Visibility=Visibility.Collapsed;return;}
   Point position=target.View.TranslatePoint(new Point(0,after?target.View.ActualHeight:0),root);Point viewport=scroll.TranslatePoint(new Point(),root);
   if(position.Y<viewport.Y||position.Y>viewport.Y+scroll.ActualHeight){marker.Visibility=Visibility.Collapsed;return;}
   marker.Width=Math.Max(0,target.View.ActualWidth);Canvas.SetLeft(marker,position.X);Canvas.SetTop(marker,position.Y-1);marker.Visibility=Visibility.Visible;
  }
  void ScrollEdge(object sender,EventArgs e){
   if(!gesture.Active||Mouse.LeftButton!=MouseButtonState.Pressed){Cancel();return;}Point p=Mouse.GetPosition(scroll);double delta=0;
   if(p.X>=0&&p.X<=scroll.ActualWidth&&p.Y>=-24&&p.Y<=scroll.ActualHeight+24){if(p.Y<36)delta=-Math.Min(22,(36-p.Y)*.55);else if(p.Y>scroll.ActualHeight-36)delta=Math.Min(22,(p.Y-scroll.ActualHeight+36)*.55);}
   if(delta!=0){scroll.ScrollToVerticalOffset(scroll.VerticalOffset+delta);scroll.UpdateLayout();}UpdateTarget();
  }
  void OnWheel(object sender,MouseWheelEventArgs e){if(source!=null)Cancel();}
  void Finish(bool releaseCapture){
   cleaning=true;holdTimer.Stop();edgeTimer.Stop();marker.Visibility=Visibility.Collapsed;Row previous=source;source=null;target=null;gesture.Reset();
   if(previous!=null){previous.View.Opacity=1;if(previous.Handle.Cursor==Cursors.SizeNS)previous.Handle.Cursor=oldCursor;if(releaseCapture&&previous.Handle.IsMouseCaptured)previous.Handle.ReleaseMouseCapture();}cleaning=false;
  }
  public void Cancel(){Finish(true);}
  public void ResetRows(){Cancel();rows.Clear();}
  public void Dispose(){ResetRows();scroll.PreviewMouseWheel-=OnWheel;root.Children.Remove(overlay);}
 }
}
