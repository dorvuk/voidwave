using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Animator animator;
    
    [Header("Board Attachment")]
    public GameObject surfboard;
    public Transform hipsAttachPoint; // Assign mixamorig:Hips in Inspector
    public Transform boardWorldParent; // Empty GameObject in scene for detached board
    
    void Start()
    {
        animator = GetComponent<Animator>();
        
        // Start with board attached
        AttachBoard();
    }
    
    // Called by Animation Events or manually
    public void AttachBoard()
    {
        if (surfboard != null && hipsAttachPoint != null)
        {
            surfboard.transform.SetParent(hipsAttachPoint);
            surfboard.transform.localPosition = Vector3.zero; // Adjust position as needed
            surfboard.transform.localRotation = Quaternion.identity; // Adjust rotation as needed
        }
    }
    
    public void HideBoard()
    {
        if (surfboard != null)
        {
            surfboard.SetActive(false); // Hide board
        }
    }

    public void ShowBoard()
    {
        if (surfboard != null)
        {
            surfboard.SetActive(true); // Show board
        }
    }

    // Called by Animation Events or manually
    public void DetachBoard()
    {
        if (surfboard != null && boardWorldParent != null)
        {
            surfboard.transform.SetParent(boardWorldParent);
            // Board maintains its world position when detached
        }
    }
    
    // Game state control methods
    public void StartGame()
    {
        animator.SetBool("isGameStarted", true);
    }
    
    public void HitObstacle()
    {
        animator.SetTrigger("isHit");
    }
    
    public void Die()
    {
        animator.SetBool("isDead", true);
    }
}
