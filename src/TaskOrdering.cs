using System;
using System.Collections.Generic;
using System.Linq;

namespace FrostTodo {
 public static class TaskOrdering {
  // Refill only the visible slots; filtered-out and completed tasks keep their positions.
  public static bool Move(List<Todo> tasks,IEnumerable<string> visibleIds,string sourceId,string targetId,bool after){
   if(sourceId==targetId)return false;
   HashSet<string> visible=new HashSet<string>(visibleIds);
   List<Todo> ordered=tasks.Where(t=>visible.Contains(t.Id)).ToList();
   int from=ordered.FindIndex(t=>t.Id==sourceId),target=ordered.FindIndex(t=>t.Id==targetId);
   if(from<0||target<0)return false;
   string[] before=ordered.Select(t=>t.Id).ToArray();Todo moved=ordered[from];ordered.RemoveAt(from);
   int insertion=ordered.FindIndex(t=>t.Id==targetId)+(after?1:0);ordered.Insert(insertion,moved);
   if(ordered.Select(t=>t.Id).SequenceEqual(before))return false;
   int next=0;for(int index=0;index<tasks.Count;index++)if(visible.Contains(tasks[index].Id))tasks[index]=ordered[next++];
   return true;
  }
 }
 public sealed class TaskHoldGesture {
  public const int DelayMilliseconds=450;public const double MovementTolerance=8;
  double startX,startY;long started;
  public bool Pending{get;private set;}public bool Active{get;private set;}public bool CancelledByMovement{get;private set;}
  public void Press(double x,double y,long now){Reset();startX=x;startY=y;started=now;Pending=true;}
  public void Move(double x,double y){if(Pending&&Math.Sqrt((x-startX)*(x-startX)+(y-startY)*(y-startY))>MovementTolerance){Pending=false;CancelledByMovement=true;}}
  public bool Activate(long now,bool leftPressed){if(!leftPressed){Reset();return false;}if(!Pending||now-started<DelayMilliseconds)return false;Pending=false;Active=true;return true;}
  public bool Release(){bool suppressClick=Active||CancelledByMovement;Reset();return suppressClick;}
  public void Reset(){Pending=false;Active=false;CancelledByMovement=false;}
 }
}
