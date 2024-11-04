using UnityEngine;

public class SkySwitcher : MonoBehaviour
{
    public Material daySkyMaterial;
    public Material nightSkyMaterial;
    public float animationSpeed = 1f;
    public float transitionDuration = 5f; // ������������ �������� ����� �����������
    public float switchInterval = 60f; // �������� ����� � �������� (1 ������)

    private MeshRenderer meshRenderer;
    private bool isDay = true; // ���� �������� ������� �����
    private float transitionTimer = 0f;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = daySkyMaterial; // ������������� ������� �������� ����������
    }

    private void Update()
    {
        // ���������-��������
        meshRenderer.material.mainTextureOffset += new Vector2(animationSpeed * Time.deltaTime, 0);

        // ��������� ������
        transitionTimer += Time.deltaTime;

        // ���������, ���� ������ ����������� ����� ��� �����
        if (transitionTimer >= switchInterval)
        {
            transitionTimer = 0f; // ���������� ������
            StartCoroutine(SwitchSky()); // ��������� ������� �����
        }
    }

    private System.Collections.IEnumerator SwitchSky()
    {
        Material startMaterial = isDay ? daySkyMaterial : nightSkyMaterial;
        Material endMaterial = isDay ? nightSkyMaterial : daySkyMaterial;

        float elapsedTime = 0f;
        isDay = !isDay; // ����������� ����

        // ������ �������� �������� � �������������� �������� ������������ (Lerp)
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float blendFactor = Mathf.Clamp01(elapsedTime / transitionDuration); // ��������������� ������ (�� 0 �� 1)

            // ������� ������������� ����� ����� ����������
            meshRenderer.material.Lerp(startMaterial, endMaterial, blendFactor);

            yield return null;
        }

        // ������������� �������� ��������
        meshRenderer.material = endMaterial;
    }
}
