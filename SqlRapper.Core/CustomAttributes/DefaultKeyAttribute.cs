using System;

namespace SqlRapper.CustomAttributes
{
    /// <summary>
    /// This default Key attribute can be put over any property name that is going into the db.  It designates a default key and that
    /// default key property will be ignored during an insert/update IF it is also null.  For more information see the SqlDataService.
    /// </summary>
    public class DefaultKeyAttribute : Attribute
    {
    }
}
