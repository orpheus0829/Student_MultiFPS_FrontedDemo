using UnityEngine;

public interface IWorldInteraction
{
    bool CanInteract(GameObject interactor);

    void Interact(GameObject interactor);
}
