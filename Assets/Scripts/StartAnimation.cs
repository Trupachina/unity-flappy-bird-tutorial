using UnityEngine;

public class StartAnimation : MonoBehaviour
{
    public Animator animator;
    [SerializeField] private string animationName = "logo_anim"; // Название анимации

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (animator != null)
        {
            animator.Play(animationName); // Запускает анимацию по имени
        }
    }
}
