namespace Aurora.Core;
public sealed class EditHistory<T>(Func<T,T,bool> equal,Func<T,long> size,int limit=48,long budget=192L*1024*1024,Func<T,T,long>? changeSize=null)
{
 readonly LinkedList<(T Before,T After,long Size)> undo=[];
 readonly Stack<(T Before,T After,long Size)> redo=[];
 long used;
 public bool CanUndo=>undo.Count>0;
 public bool CanRedo=>redo.Count>0;
 public void Clear(){undo.Clear();redo.Clear();used=0;}
 public void Commit(T before,T after){if(equal(before,after))return;long cost=changeSize==null?checked(size(before)+size(after)):changeSize(before,after);if(cost<0)throw new InvalidOperationException("Invalid history change size");foreach(var item in redo)used-=item.Size;redo.Clear();undo.AddLast((before,after,cost));used=checked(used+cost);while(undo.Count>1&&(undo.Count>limit||used>budget)){used-=undo.First!.Value.Size;undo.RemoveFirst();}}
 public T Undo(){if(undo.Last==null)throw new InvalidOperationException("Nothing to undo");var entry=undo.Last.Value;undo.RemoveLast();redo.Push(entry);return entry.Before;}
 public T Redo(){if(redo.Count==0)throw new InvalidOperationException("Nothing to redo");var entry=redo.Pop();undo.AddLast(entry);return entry.After;}
}
