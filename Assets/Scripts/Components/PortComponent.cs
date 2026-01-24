using UnityEngine;

public class PortComponent : ComponentBase
{
    // Direction this port faces relative to the grid center
    // Up Port is at (3, 6), facing Up (Outwards)
    public Direction wallDirection;
    
    public RecursiveModuleComponent parentModule; // Link to the outer shell

    // Helper to auto-configure based on position
    public void Configure(RecursiveModuleComponent parent, Direction dir)
    {
        parentModule = parent;
        wallDirection = dir;
        // Visuals can be updated here to show arrow pointing IN or OUT?
        // For now, simple box.
    }

    protected override void Start()
    {
        // Do NOT call base.Start() because we are out of bounds (-1 or 7).
        // We handle our own lifecycle or attached to InnerGrid transform manually.
        
        // Find manager for manual component lookup usage if needed
        if (_assignedManager == null)
            _assignedManager = GetComponentInParent<ModuleManager>();
            
        // Manually register to Tick system
        if (TickManager.Instance != null)
        {
            TickManager.Instance.OnTick += OnTick;
        }
    }

    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        // When item arrives at Port from INSIDE the module:
        // It wants to leave the module.
        // We pass it to the parentModule to handle external dispensing.
        
        if (parentModule != null)
        {
            return parentModule.ExportItem(word, wallDirection);
        }
        return false;
    }

    // Called by Parent Module to spawn item coming from Outside
    public bool ImportItem(WordData word)
    {
        // Item entering from outside.
        // We need to push it into the inner grid (Opposite of wallDirection).
        // e.g. Top Port (Up) -> Push Down (Inner).
        
        // Just hold it? Or push immediately?
        // ComponentBase default logic pushes to "OutputDirection".
        // We should set our RotationIndex such that OutputDirection is Inwards.
        
        // Top Port (Up Wall): Needs to push Down. Rotation should be Down (2).
        // Right Port: Push Left. Rotation Left (3).
        
        // We can just hold it, and OnTick will push it.
        if (HeldWord == null)
        {
            HeldWord = word;
            UpdateVisuals();
            return true;
        }
        return false;
    }
}
