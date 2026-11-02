using Godot;
public partial class GranaryWallReference:Node2D {
 public override async void _Ready(){try{
 var plane=new Node2D{Scale=Vector2.One*7};AddChild(plane);plane.AddChild(new Sprite2D{Texture=ResourceLoader.Load<Texture2D>("res://assets/native/scenes/luojia.png"),Centered=false});
 var pts=new[]{new Vector2(303,229),new Vector2(314,236),new Vector2(366,260),new Vector2(376,265),new Vector2(326,238),new Vector2(397,263),new Vector2(314.5f,212.5f)};
 for(int i=0;i<pts.Length;i++){var p=pts[i];plane.AddChild(new Line2D{Points=new[]{p-new Vector2(2,0),p+new Vector2(2,0)},Width=.45f,DefaultColor=i==6?Colors.Red:Colors.Cyan});plane.AddChild(new Line2D{Points=new[]{p-new Vector2(0,2),p+new Vector2(0,2)},Width=.45f,DefaultColor=i==6?Colors.Red:Colors.Cyan});var label=new Label{Text=$"{i+1}",Position=p+new Vector2(1,-5),Scale=Vector2.One*.23f};label.AddThemeColorOverride("font_color",Colors.Black);plane.AddChild(label);}
 var ui=new CanvasLayer();AddChild(ui);ui.AddChild(new Label{Text="ORIGINAL SOURCE PIXELS | geometry investigation only, no gameplay\n1=303,229  2=314,236  3=366,260  4=376,265  5=326,238  6=397,263  RED7=314.5,212.5",Position=new(10,10)});
 void Layout(){plane.Position=GetViewportRect().Size/2-new Vector2(350,230)*7;}GetViewport().SizeChanged+=Layout;Layout();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/workspace/scratch/84c9fe948755/c07-static-preview/granary-wall-original-pixel-witness.png");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
