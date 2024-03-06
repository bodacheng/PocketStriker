using System.Linq;

public static class Copier<TParent, TChild> where TParent : class
    where TChild : class
{
    public static void Copy(TParent parent, TChild child)
    {
        var parentFields = parent.GetType().GetFields();
        var childFields = child.GetType().GetFields();

        var childFieldsDict = childFields.ToDictionary(field => field.Name);

        foreach (var parentField in parentFields)
        {
            if (childFieldsDict.TryGetValue(parentField.Name, out var childField)
                && parentField.FieldType == childField.FieldType)
            {
                childField.SetValue(child, parentField.GetValue(parent));
            }
        }
    }
}