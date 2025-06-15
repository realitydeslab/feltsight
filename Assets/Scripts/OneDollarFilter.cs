using UnityEngine;

/// <summary>
/// OneDollar滤波器 - 用于平滑数据流
/// 这是一个简单的低通滤波器，可以有效减少噪声
/// </summary>
// [System.Serializable]
// public class OneDollarFilter
public class OneDollarFilter : MonoBehaviour
{
    [SerializeField] [Range(0.01f, 1.0f)]
    private float m_FilterStrength = 0.1f; // 滤波强度，值越小滤波效果越强
    
    private Vector3 m_FilteredValue = Vector3.zero;
    private bool m_IsInitialized = false;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filterStrength">滤波强度 (0.01-1.0)，值越小滤波效果越强</param>
    public OneDollarFilter(float filterStrength = 0.1f)
    {
        m_FilterStrength = Mathf.Clamp(filterStrength, 0.01f, 1.0f);
    }

    /// <summary>
    /// 设置滤波强度
    /// </summary>
    /// <param name="strength">滤波强度 (0.01-1.0)</param>
    public void SetFilterStrength(float strength)
    {
        m_FilterStrength = Mathf.Clamp(strength, 0.01f, 1.0f);
    }

    /// <summary>
    /// 获取当前滤波强度
    /// </summary>
    public float GetFilterStrength()
    {
        return m_FilterStrength;
    }

    /// <summary>
    /// 应用滤波到Vector3数据
    /// </summary>
    /// <param name="newValue">新的输入值</param>
    /// <returns>滤波后的值</returns>
    public Vector3 Filter(Vector3 newValue)
    {
        if (!m_IsInitialized)
        {
            m_FilteredValue = newValue;
            m_IsInitialized = true;
            return m_FilteredValue;
        }

        // OneDollar滤波公式: filtered = filtered + α * (new - filtered)
        // 其中α是滤波强度
        m_FilteredValue = m_FilteredValue + m_FilterStrength * (newValue - m_FilteredValue);
        return m_FilteredValue;
    }

    /// <summary>
    /// 应用滤波到float数据
    /// </summary>
    /// <param name="newValue">新的输入值</param>
    /// <returns>滤波后的值</returns>
    public float Filter(float newValue)
    {
        if (!m_IsInitialized)
        {
            m_FilteredValue.x = newValue;
            m_IsInitialized = true;
            return m_FilteredValue.x;
        }

        m_FilteredValue.x = m_FilteredValue.x + m_FilterStrength * (newValue - m_FilteredValue.x);
        return m_FilteredValue.x;
    }

    /// <summary>
    /// 重置滤波器
    /// </summary>
    public void Reset()
    {
        m_IsInitialized = false;
        m_FilteredValue = Vector3.zero;
    }

    /// <summary>
    /// 获取当前滤波后的值
    /// </summary>
    public Vector3 GetCurrentValue()
    {
        return m_FilteredValue;
    }
}
