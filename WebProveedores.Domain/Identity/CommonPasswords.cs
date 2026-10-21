namespace WebProveedores.Domain.Identity;

/// <summary>
/// Contraseñas y palabras demasiado comunes (en minúsculas y sin tildes): las más usadas en filtraciones públicas,
/// palabras frecuentes en español, secuencias de teclado y términos del negocio. <see cref="PasswordPolicy"/> las
/// compara también sin los números y símbolos de los extremos («Naviera2026!» → «naviera»).
/// </summary>
public static class CommonPasswords
{
    private static readonly HashSet<string> Words =
    [
        // Negocio y portal
        "naviera", "transoceanica", "navieratransoceanica", "petrolera", "petroleratransoceanica", "petral", "navierapetral",
        "navitranso", "transoceanico", "proveedor", "proveedores", "portal", "portalproveedores", "webproveedores", "sap",
        "empresa", "compania", "usuario", "usuarios", "administrador", "admin", "administrator", "root", "sistema", "soporte",
        "factura", "facturas", "pagos", "contabilidad", "compras", "logistica", "buque", "barco", "puerto", "callao", "peru", "lima",

        // Contraseñas más usadas
        "password", "passw0rd", "passwd", "pass", "contrasena", "contrasenia", "clave", "claves", "secreto", "acceso", "login",
        "welcome", "bienvenido", "bienvenida", "letmein", "iloveyou", "teamo", "tequiero", "amor", "amorcito", "princesa",
        "princess", "dragon", "monkey", "master", "shadow", "sunshine", "superman", "batman", "football", "baseball", "soccer",
        "futbol", "trustno1", "starwars", "pokemon", "whatever", "freedom", "qazwsx", "michael", "jordan", "charlie", "hello",
        "hola", "holamundo", "changeme", "default", "temporal", "temporary", "prueba", "pruebas", "test", "testing", "demo",
        "invierno", "verano", "primavera", "otono", "winter", "summer", "spring", "autumn", "enero", "febrero", "marzo", "abril",
        "mayo", "junio", "julio", "agosto", "septiembre", "setiembre", "octubre", "noviembre", "diciembre", "lunes", "martes",
        "miercoles", "jueves", "viernes", "sabado", "domingo", "familia", "mama", "papa", "hijo", "hija", "dios", "jesus",
        "jesucristo", "angel", "angelito", "estrella", "corazon", "mariposa", "chocolate", "tequila", "pisco", "cerveza",
        "perro", "gato", "tigre", "leon", "alianza", "alianzalima", "universitario", "cristal", "sporting", "barcelona",
        "realmadrid", "madrid", "argentina", "colombia", "mexico", "chile", "espana",

        // Secuencias de teclado y de caracteres
        "qwerty", "qwertyuiop", "qwertyu", "qwert", "asdf", "asdfgh", "asdfghjkl", "zxcvbn", "zxcvbnm", "azerty", "abc", "abcd",
        "abcde", "abcdef", "abcdefg", "abcdefgh", "abcdefghijkl", "aaaaaa", "qweasd", "qweasdzxc", "1q2w3e4r", "1qaz2wsx",
        "123456", "1234567", "12345678", "123456789", "1234567890", "12345678910", "123456789012", "000000", "111111",
        "121212", "123123", "654321", "666666", "696969", "987654321", "112233", "123321", "abc123", "password1",
    ];

    /// <summary><paramref name="normalized"/>: contraseña o palabra en minúsculas y sin tildes.</summary>
    public static bool Contains(string normalized) => Words.Contains(normalized);
}
