using UnityEngine;
using UnityEngine.UI;

public class BoardPieces : MonoBehaviour
{
    [SerializeField] Canvas canvas;
    [SerializeField] Text HPText;
    [SerializeField] Text attackText;
    [SerializeField] Text dodgeText;
    public bool isGhost;

    public ConflictSide conflictSide;
    public MovementPattern movementPattern;
    public int HP;
    public int firstAttack;
    public int attack;
    public float dodge;
    public byte neverMoved;

    void Start()
    {
        HPText.text = $"HP: {HP}";
        attackText.text = $"Attack: {attack}";
        dodgeText.text = $"Dodge: {dodge}";
        canvas.gameObject.SetActive(false);
    }

    void OnMouseEnter()
    {
        if (isGhost)
        {
            return;
        }
        canvas.gameObject.SetActive(true);
    }

    void OnMouseExit()
    {
        if (isGhost)
        {
            return;
        }
        canvas.gameObject.SetActive(false);
    }
}
