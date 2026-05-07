using System;

public static class NumerosALetras
{
    /// <summary>
    /// Convierte un monto a letras con moneda.
    /// </summary>
    /// <param name="monto">Monto decimal</param>
    /// <param name="moneda">Código de moneda, ejemplo: "MXN", "USD", "EUR"</param>
    /// <param name="idioma">"es" para español, "en" para inglés</param>
    /// <returns>Monto en letras con moneda</returns>
    public static string ConvertirConMoneda(decimal monto, string moneda = "MXN", string idioma = "es")
    {
        // Separar parte entera y decimal
        long entero = (long)Math.Floor(monto);
        int decimales = (int)Math.Round((monto - entero) * 100);

        string letras = "";

        if (idioma.ToLower() == "es")
        {
            letras = NumeroALetrasEs(entero);

            // FORMATO ESPECIAL PARA M.N. (Moneda Nacional)
            if (moneda.ToUpper() == "M.N." || moneda.ToUpper() == "MXN" || moneda.ToUpper() == "MXP")
            {
                letras = CapitalizarPrimeraLetra(letras);

                if (decimales > 0)
                {
                    letras += " " + decimales.ToString("00") + "/100";
                }

                letras += " M.N."; // ← Moneda al final
            }
            else
            {
                // Formato normal para otras monedas
                string monedaTexto = ObtenerMonedaEs(moneda, entero);
                letras = CapitalizarPrimeraLetra(letras) + " " + monedaTexto;

                if (decimales > 0)
                {
                    letras += " " + decimales.ToString("00") + "/100";
                }
            }
        }
        else if (idioma.ToLower() == "en")
        {
            letras = NumeroALetrasEn(entero);
            string monedaTexto = ObtenerMonedaEn(moneda, entero);

            // Formato corregido para inglés
            letras = CapitalizarPrimeraLetra(letras) + " " + monedaTexto;

            if (decimales > 0)
            {
                letras += " AND " + decimales.ToString("00") + "/100";
            }
        }
        else
        {
            throw new ArgumentException("Idioma no soportado. Usar 'es' o 'en'.");
        }

        return letras;
    }

    private static string CapitalizarPrimeraLetra(string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return texto;

        return char.ToUpper(texto[0]) + texto.Substring(1);
    }

    #region Español
    private static string NumeroALetrasEs(long numero)
    {
        if (numero == 0) return "cero";

        string[] unidades = { "", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve",
                               "diez", "once", "doce", "trece", "catorce", "quince",
                               "dieciséis", "diecisiete", "dieciocho", "diecinueve" };
        string[] decenas = { "", "", "veinte", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };
        string[] centenas = { "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos",
                              "seiscientos", "setecientos", "ochocientos", "novecientos" };

        string resultado = "";

        // Manejar millones
        if (numero >= 1000000)
        {
            long millones = numero / 1000000;
            resultado += NumeroALetrasEs(millones) + " millón";
            if (millones > 1) resultado += "es";
            numero %= 1000000;
            if (numero > 0) resultado += " ";
        }

        // Manejar miles
        if (numero >= 1000)
        {
            long miles = numero / 1000;
            if (miles == 1)
                resultado += "mil";
            else
                resultado += NumeroALetrasEs(miles) + " mil";
            numero %= 1000;
            if (numero > 0) resultado += " ";
        }

        // Manejar centenas
        if (numero >= 100)
        {
            if (numero == 100)
                resultado += "cien";
            else
            {
                long cent = numero / 100;
                resultado += centenas[cent];
            }
            numero %= 100;
            if (numero > 0) resultado += " ";
        }

        // Manejar decenas y unidades
        if (numero >= 20)
        {
            long dec = numero / 10;
            resultado += decenas[dec];
            numero %= 10;
            if (numero > 0)
                resultado += " y " + unidades[numero];
        }
        else if (numero > 0)
        {
            resultado += unidades[numero];
        }

        return resultado;
    }

    private static string ObtenerMonedaEs(string moneda, long entero)
    {
        switch (moneda.ToUpper())
        {
            case "MXN":
            case "M.N.":
            case "MXP":
                return ""; // ← Ya no devuelve texto porque va al final como "M.N."
            case "USD":
                return entero == 1 ? "dólar" : "dólares";
            case "EUR":
                return entero == 1 ? "euro" : "euros";
            case "GBP":
                return entero == 1 ? "libra esterlina" : "libras esterlinas";
            case "CAD":
                return entero == 1 ? "dólar canadiense" : "dólares canadienses";
            case "JPY":
                return entero == 1 ? "yen" : "yenes";
            case "CNY":
                return entero == 1 ? "yuan" : "yuanes";
            case "BRL":
                return entero == 1 ? "real" : "reales";
            default:
                return moneda.ToUpper(); // Para monedas no especificadas
        }
    }
    #endregion

    #region Inglés
    private static string NumeroALetrasEn(long numero)
    {
        if (numero == 0) return "zero";

        string[] unidades = { "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
                               "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen",
                               "sixteen", "seventeen", "eighteen", "nineteen" };
        string[] decenas = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

        string resultado = "";

        // Manejar millones
        if (numero >= 1000000)
        {
            long millones = numero / 1000000;
            resultado += NumeroALetrasEn(millones) + " million";
            numero %= 1000000;
            if (numero > 0) resultado += " ";
        }

        // Manejar miles
        if (numero >= 1000)
        {
            long miles = numero / 1000;
            resultado += NumeroALetrasEn(miles) + " thousand";
            numero %= 1000;
            if (numero > 0) resultado += " ";
        }

        // Manejar centenas
        if (numero >= 100)
        {
            long cent = numero / 100;
            resultado += unidades[cent] + " hundred";
            numero %= 100;
            if (numero > 0) resultado += " ";
        }

        // Manejar decenas y unidades
        if (numero >= 20)
        {
            long dec = numero / 10;
            resultado += decenas[dec];
            numero %= 10;
            if (numero > 0)
                resultado += "-" + unidades[numero];
        }
        else if (numero > 0)
        {
            resultado += unidades[numero];
        }

        return resultado;
    }

    private static string ObtenerMonedaEn(string moneda, long entero)
    {
        switch (moneda.ToUpper())
        {
            case "MXN":
            case "M.N.":
            case "MXP":
                return entero == 1 ? "Mexican peso" : "Mexican pesos";
            case "USD":
                return entero == 1 ? "US dollar" : "US dollars";
            case "EUR":
                return entero == 1 ? "euro" : "euros";
            case "GBP":
                return entero == 1 ? "British pound" : "British pounds";
            case "CAD":
                return entero == 1 ? "Canadian dollar" : "Canadian dollars";
            case "JPY":
                return entero == 1 ? "yen" : "yen";
            case "CNY":
                return entero == 1 ? "yuan" : "yuan";
            case "BRL":
                return entero == 1 ? "Brazilian real" : "Brazilian reals";
            default:
                return moneda.ToUpper(); // Para monedas no especificadas
        }
    }
    #endregion
}