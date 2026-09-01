using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;

public class Crystal_Skill : Skill
{
    [SerializeField] private float crystalDuration;
    [SerializeField] private GameObject crystalPrefab;
    private GameObject currentCrystal;

    [Header("Crystal simple")]
    [SerializeField] private UI_SkillTreeSlot unlockCrystalButton;
    public bool crystalUnlocked { get; private set; }

    [Header("Crystal mirage")]
    [SerializeField] private UI_SkillTreeSlot unlockCloneInsteadButton;
    [SerializeField] private bool cloneInsteadOfCrystal;

    [Header("Explosive crystal")]
    [SerializeField] private UI_SkillTreeSlot unlockExplosiveButton;
    [SerializeField] private float explosiveCooldown;
    [SerializeField] private bool canExplode;

    [Header("Moving crystal")]
    [SerializeField] private UI_SkillTreeSlot unlockMovingCrystalButton;
    [SerializeField] private bool canMoveToEnemy;
    [SerializeField] private float moveSpeed;

    [Header("Multi stacking crystal")]
    [SerializeField] private UI_SkillTreeSlot unlockMultiStackButton;
    [SerializeField] private bool canUseMultiStacks;
    [SerializeField] private int amountOfStacks;
    [SerializeField] private float multiStackCoolDown;
    [SerializeField] private float refillCoolDown;
    [SerializeField]private float refillTimer;
    [SerializeField] private List<GameObject> crystalList = new List<GameObject>();

    //热更新
    [SerializeField]
    private AssetReference crystalReference;

    protected override void Start()
    {
        base.Start();

        unlockCrystalButton.GetComponent<Button>().onClick.AddListener(UnlockCrystal);
        unlockCloneInsteadButton.GetComponent<Button>().onClick.AddListener(UnlockCrystalMirage);
        unlockExplosiveButton.GetComponent<Button>().onClick.AddListener(UnlockExplosiveCrystal);
        unlockMovingCrystalButton.GetComponent<Button>().onClick.AddListener(UnlockMovingCrystal);
        unlockMultiStackButton.GetComponent<Button>().onClick.AddListener(UnlockMultiStack);

        crystalReference.InstantiateAsync().Completed += (Handheld) => Debug.Log("done");
    }

    #region 技能解锁

    protected override void CheckUnlock()
    {
        UnlockCrystal();
        UnlockCrystalMirage();
        UnlockExplosiveCrystal();
        UnlockMovingCrystal();
        UnlockMultiStack();
    }
    private void UnlockCrystal()
    {
        if (unlockCrystalButton.unlocked)
            crystalUnlocked = true;
    }

    private void UnlockCrystalMirage()
    {
        if (unlockCloneInsteadButton.unlocked)
            cloneInsteadOfCrystal = true;
    }

    private void UnlockExplosiveCrystal()
    {
        if (unlockExplosiveButton.unlocked)
        {
            canExplode = true;
            cooldown = explosiveCooldown;
        }
    }

    private void UnlockMovingCrystal()
    {
        if (unlockMovingCrystalButton.unlocked)
            canMoveToEnemy = true;
    }

    private void UnlockMultiStack()
    {
        if (unlockMovingCrystalButton.unlocked)
            canUseMultiStacks = true;
    }

    #endregion

    protected override void Update()
    {
        base.Update();

        if (!canUseMultiStacks) return;

        refillTimer -= Time.deltaTime;
        if (refillTimer < 0 && crystalList.Count < amountOfStacks)
            RefillCrystal();
    }
    public override void UseSkill()
    {
        base.UseSkill();

        if (CanUseMultiCrystal())
            return;

        if (currentCrystal == null)
        {
            CreateCrystal();
        }
        else
        {
            if (canMoveToEnemy)
                return;

            Vector2 playerPos = player.transform.position;
            player.transform.position = currentCrystal.transform.position;
            currentCrystal.transform.position = playerPos;

            if(cloneInsteadOfCrystal)
            {
                SkillManager.instance.clone.CreateClone(currentCrystal.transform, Vector3.zero);
                Destroy(currentCrystal);
            }
            else
            {
                currentCrystal.GetComponent<Crystal_Skill_Controller>()?.FinishCrystal();
            }
        }
    }

    public void CreateCrystal()
    {
        currentCrystal = Instantiate(crystalPrefab, player.transform.position, Quaternion.identity);
        Crystal_Skill_Controller currentCrystalScript = currentCrystal.GetComponent<Crystal_Skill_Controller>();

        currentCrystalScript.SetupCrystal(player, crystalDuration, canExplode, canMoveToEnemy, moveSpeed, FindClosestEnemy(currentCrystal.transform));
    }

    public void CurrentCrystalChooseRandomTarget() => currentCrystal.GetComponent<Crystal_Skill_Controller>().ChooseRandomEnemy();

    private bool CanUseMultiCrystal()
    {
        if (canUseMultiStacks)
        {
            if(crystalList.Count > 0)
            {
                cooldown = multiStackCoolDown;

                GameObject crystalToSpawn = crystalList[crystalList.Count - 1];
                GameObject newCrystal = Instantiate(crystalToSpawn,player.transform.position, Quaternion.identity);

                crystalList.Remove(crystalToSpawn);

                newCrystal.GetComponent<Crystal_Skill_Controller>().
                    SetupCrystal(player, crystalDuration,canExplode,canMoveToEnemy,moveSpeed,FindClosestEnemy(newCrystal.transform));

                return true;

            }

        }

        return false;
    }

    private void RefillCrystal()
    {
        crystalList.Add(crystalPrefab);
        refillTimer = refillCoolDown;
    }

}
