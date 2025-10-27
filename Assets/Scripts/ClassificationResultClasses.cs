using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 有效的分类结果类
/// </summary>
public class ValidClassificationResult : ClassificationResultBase
{
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    
    public override bool IsValid()
    {
        return true;
    }
    
    public override string GetDisplayText()
    {
        return $"{Label} ({MaterialType})";
    }
    
    public override Color GetDisplayColor()
    {
        // 根据材质类型返回不同颜色
        return MaterialType switch
        {
            "M1" => new Color(1.0f, 0.98f, 0.41f),  // 金属
            "M2" => new Color(0.41f, 0.84f, 1.0f),  // 玻璃/陶瓷
            "M3" => new Color(0.67f, 0.64f, 1.0f),  // 硬塑
            "M4" => new Color(1.0f, 0.78f, 0.43f),  // 木材
            "M5" => new Color(0.95f, 0.95f, 0.91f), // 石材/水泥
            "M6" => new Color(0.93f, 0.71f, 1.0f),  // 织物/毛皮
            "M7" => new Color(1.0f, 0.71f, 0.63f),  // 皮革/橡胶
            "M8" => new Color(0.88f, 1.0f, 0.74f),  // 纸/纸板
            "M9" => new Color(1.0f, 0.87f, 0.99f),  // 食物软组织
            "M10" => new Color(0.64f, 1.0f, 0.96f), // 植被/土壤
            "M11" => new Color(0.64f, 0.77f, 1.0f), // 电子玻璃面板
            "M12" => new Color(0.62f, 1.0f, 0.7f),  // 泡沫/海绵/复合
            _ => Color.white
        };
    }
}

/// <summary>
/// 无效的分类结果类（用于表示未找到检测框的情况）
/// </summary>
public class InvalidClassificationResult : ClassificationResultBase
{
    public override bool IsValid()
    {
        return false;
    }
    
    public override string GetDisplayText()
    {
        return "None";
    }
    
    public override Color GetDisplayColor()
    {
        return Color.gray;
    }
}