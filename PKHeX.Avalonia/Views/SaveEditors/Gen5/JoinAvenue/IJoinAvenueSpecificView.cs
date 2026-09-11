using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5.JoinAvenue;

/// <summary>The per-kind half of a Join Avenue entity editor (port of <c>IJoinAvenueSpecificEditor</c>).</summary>
internal interface IJoinAvenueSpecificView<in T> where T : class, IJoinAvenueEntity5
{
    void LoadObject(T entity);
    void SaveObject(T entity);
}
