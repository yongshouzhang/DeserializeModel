using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReaderTest.Model
{
    public class Person
    {

        /// <summary>
        /// Primary key for Person records.
        /// </summary>
        public int BusinessEntityID { get; set; }
        /// <summary>
        /// Primary type of person: SC = Store Contact, IN = Individual (retail) customer, SP = Sales person, EM = Employee (non-sales), VC = Vendor contact, GC = General contact
        /// </summary>
        public string PersonType { get; set; }
        /// <summary>
        /// A courtesy title. For example, Mr. or Ms.
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// First name of the person.
        /// </summary>
        public string FirstName { get; set; }
        /// <summary>
        /// Middle name or middle initial of the person.
        /// </summary>
        public string MiddleName { get; set; }
        /// <summary>
        /// Last name of the person.
        /// </summary>
        public string LastName { get; set; }
        /// <summary>
        /// ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.
        /// </summary>
        public Guid rowguid { get; set; }

    }
}
