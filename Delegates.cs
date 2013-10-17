using System;
using System.Collections.Generic;
using System.Text;

namespace Leleko.CSharp.ComponentModel
{
    /// <summary>
    /// Делегат gettera
    /// </summary>
    /// <param name="obj">объект</param>
    /// <param name="value">значение</param>
    public delegate void Setter<TObject, TValue>(TObject obj, TValue value);

    /// <summary>
    /// Делегат settera
    /// </summary>
    /// <param name="obj">объект</param>
    /// <returns>значение</returns>
    public delegate TValue Getter<TObject, TValue>(TObject obj);
}
