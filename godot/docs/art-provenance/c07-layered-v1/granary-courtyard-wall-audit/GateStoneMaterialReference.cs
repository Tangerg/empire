using Godot;
public partial class GateStoneMaterialReference:Node2D {
 public override async void _Ready(){try{
 var plane=new Node2D{Scale=Vector2.One*7};AddChild(plane);plane.AddChild(new Sprite2D{Texture=ResourceLoader.Load<Texture2D>("res://assets/native/c07-layered-v1/southeast-gate-v2-stone-candidate.png"),Centered=false});
 var ui=new CanvasLayer();AddChild(ui);ui.AddChild(new Label{Text="EXISTING GATE STONE MATERIAL | atlas inspection, no gameplay\nView centre480,1000 zoom7; unedited texture sample",Position=new(10,10)});
 void Layout(){plane.Position=GetViewportRect().Size/2-new Vector2(480,1000)*7;}GetViewport().SizeChanged+=Layout;Layout();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/workspace/scratch/84c9fe948755/c07-static-preview/gate-stone-material-reference.png");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
