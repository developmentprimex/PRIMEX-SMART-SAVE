using PrimEx.SmartSave.Core.Models;
namespace PrimEx.SmartSave.Desktop.ViewModels;

public sealed class TransferChoiceViewModel(TransferMode initialMode = TransferMode.Move) : ObservableObject
{
    private bool copy = initialMode == TransferMode.Copy;
    public bool CopySelected
    {
        get => copy;
        set
        {
            if (copy == value) return;
            Set(ref copy, value); Changed(nameof(MoveSelected)); Changed(nameof(Mode));
            Changed(nameof(ConfirmLabel)); Changed(nameof(Explanation));
        }
    }
    public bool MoveSelected { get => !copy; set { if (value) CopySelected = false; } }
    public TransferMode Mode => copy ? TransferMode.Copy : TransferMode.Move;
    public string ConfirmLabel => copy ? "_Copy here" : "_Move here";
    public string Explanation => copy
        ? "Copy: the original remains in its current folder. A verified copy is saved in Files PX."
        : "Move: the original is removed from its current folder only after the Files PX copy is verified.";
}
