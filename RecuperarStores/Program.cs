using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RecuperarStores
{
    class Program
    {
        static void Main(string[] args)
        {
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["BaseDeDatos"].ConnectionString;
            string rutaInicial = System.Configuration.ConfigurationManager.AppSettings["ruta"];
            // Provide the query string with a parameter placeholder.
            string queryString = "select" +
                "   p.name as stored_name, p.[type] as typeObject, " +
                "   definition as definition,  " +
                "   s.name as schema_name  " +
                "from sys.objects p  " +
                "   join sys.sql_modules c on p.object_id = c.object_id  " +
                "   join sys.schemas as s on p.schema_id = s.schema_id  " +
                "where p.[type] in ('P','V') AND p.name not like 'sp_%'  ";
            List<procedimientosModel> Procedimientos = new List<procedimientosModel>();
            using (SqlConnection connection =
                new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(queryString, connection);
                try
                {
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        //Console.WriteLine("\t{0}\t{1}\t{2}",
                        //    reader[0], reader[1], reader[2]);
                        Procedimientos.Add(new procedimientosModel()
                        {
                            stored_name = (string)reader[0],
                            typeObject = ((string)reader[1]).Trim(),
                            definition = (string)reader[2],
                            schema_name = (string)reader[3]
                        });
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.ReadKey();
                }
                
            }
            
            if(!Directory.Exists(rutaInicial))
            {
                Directory.CreateDirectory(rutaInicial);
            }
            int i = 1;
            foreach (procedimientosModel pm in Procedimientos)
            {
                Console.WriteLine($"Procesando {i++}/{Procedimientos.Count} {pm.typeObject}: {pm.schema_name}.{pm.stored_name}");
                string identificadorTipo = pm.typeObject == "P" ? "USP_" : (pm.typeObject == "V" ? "VW_" : pm.typeObject + "_");
                string rutaSchema = rutaInicial + Path.DirectorySeparatorChar + pm.schema_name;
                if (!Directory.Exists(rutaSchema))
                {
                    Directory.CreateDirectory(rutaSchema);
                }
                string nombreArchivo = rutaSchema + Path.DirectorySeparatorChar+
                    identificadorTipo + pm.stored_name + ".sql";
                File.WriteAllLines(nombreArchivo,new string[] {
                    $"IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{pm.schema_name}].[{pm.stored_name}]') AND type IN (N'{pm.typeObject}',N'{pm.typeObject}C')) ",
                    $"DROP {(pm.typeObject=="P"?"PROCEDURE":"VIEW")} {pm.schema_name}.{pm.stored_name}; ",
                    "GO "
                });
                File.AppendAllText(nombreArchivo,pm.definition);
                File.AppendAllLines(nombreArchivo,new string[]{
                    "",
                    "GO ",
                    "IF @@ERROR > 0 RAISERROR",
                    $"   ('Error al procesar {pm.typeObject}: {pm.schema_name}.{pm.stored_name}',16,127) " +
                    "ELSE PRINT " +
                    $"'{pm.typeObject} {pm.schema_name}.{pm.stored_name} procesado con éxito.'"
                });
            }
            Console.WriteLine("Terminado");
            Console.ReadKey();
            Process.Start(rutaInicial);
        }
    }
}
