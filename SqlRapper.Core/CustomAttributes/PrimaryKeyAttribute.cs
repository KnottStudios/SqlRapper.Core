using System;

namespace SqlRapper.CustomAttributes
{
    /// <summary>
    /// This primary Key attribute can be put over any property name that is going into the db.  It designates a primary key and that 
    /// primary key property will be ignored during an insert.  For more information see the SqlDataService.
    /// </summary>
    public class PrimaryKeyAttribute : Attribute
    {
    }
}
