using System.Collections.Generic;

namespace NolekMoxa
{
    /// <summary>
    /// For adding errors to a list for later use
    /// </summary>
    public class ErrorHandler
    {
        private readonly List<string> _errorList;
        /// <summary>
        /// Contstructor
        /// </summary>
        public ErrorHandler()
        {
            _errorList = new List<string>();
        }
        /// <summary>
        /// Add the specified error string to the error list
        /// </summary>
        /// <param name="error"></param>
        public void AddError(string error)
        {
            _errorList.Add(error);
        }
        /// <summary>
        /// Returns the errors
        /// </summary>
        /// <returns>An array of errors</returns>
        public string[] GetErrors()
        {
            return _errorList.ToArray();
        }

    }
}
