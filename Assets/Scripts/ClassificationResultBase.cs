using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 分类结果的抽象基类，支持多态行为
/// </summary>
public abstract class ClassificationResultBase
{
    public string Label { get; set; }
    public float Confidence { get; set; }
    public int ClassID { get; set; }
    public string MaterialType { get; set; }
    
    // 抽象方法，子类必须实现
    public abstract bool IsValid();
    
    // 虚方法，子类可以重写
    public virtual string GetDisplayText()
    {
        return Label ?? "Unknown";
    }
    
    // 虚方法，子类可以重写
    public virtual Color GetDisplayColor()
    {
        return Color.white;
    }
}