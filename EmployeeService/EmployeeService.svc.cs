using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Web.UI.WebControls;
using Newtonsoft.Json;

namespace EmployeeService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class Service1 : IEmployeeService
    {
        private const string ConnectionString = "Server=localhost;Database=try;Integrated Security=True;";

        // enable parameter is redundant
        public void EnableEmployee(int id)
        {
            var query = "UPDATE Employee SET Enable = Enable ^ 1 WHERE ID = @ID";
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection
                    .OpenWithCommand(
                        query,
                        new SqlParameter("@ID", id))
                    .Execute();
            }
        }


        //OPTION 1, fetch all data and use DFS via recursion to create employee tree
        public string GetEmployeeById(int id)
        {
            Employee employee;

            var employees = GetAllEmployees();

            employee = employees.FirstOrDefault(e => e.ID == id);
            
            if (employee != null)
            {
                BuildEmployeeTree(employee, employees); 
            }

            return JsonConvert.SerializeObject(employee);
        }
        
        private void BuildEmployeeTree(Employee vertex, List<Employee> employees)
        {
            //BETTER TIME COMPLEXITY - WORSE MEMORY COMPLEXITY (and for be better readability)
            /*var subEmployees = employees.Where(e => e.ManagerID == vertex.ID).ToList();

            if (subEmployees.Count == 0)
                return;
            
            vertex.Employees = subEmployees;*/

            //BETTER MEMORY COMPLEXITY - WORSE TIME COMPLEXITY
            if (!employees.Any(e => e.ManagerID == vertex.ID))
                return;
            
            vertex.Employees = employees.Where(e => e.ManagerID == vertex.ID).ToList();

            foreach (var employee in vertex.Employees)
            {
                BuildEmployeeTree(employee, employees);
            }
        }

        private List<Employee> GetAllEmployees()
        {
            List<Employee> employees;   

            var query = "SELECT * FROM Employee";
            Func<SqlDataReader, Employee> creatorFunc = (r) =>
            {
                return new Employee
                {
                    ID = r.GetInt32(0),
                    Name = r.GetString(1),
                    ManagerID = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                };
            };

            using (var connection = new SqlConnection(ConnectionString))
            {
                employees = connection
                    .OpenWithCommand(query)
                    .QueryMany(creatorFunc);

                connection.Dispose();
            }

            return employees;
        }


        //OPTION 2 LESS LOAD ON API SIDE, BUT MULTIPLE DB CONNECTIONS, WHICH IS WORSE
        /*
        public string GetEmployeeById(int id)
        {
            Employee employee;

            var query = "SELECT * FROM Employee WHERE ID = @ID";
            Func<SqlDataReader, Employee> creatorFunc = (r) =>
            {
                return new Employee
                {
                    ID = r.GetInt32(0),
                    Name = r.GetString(1),
                    ManagerID = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                };
            };

            using (var connection = new SqlConnection(ConnectionString))
            {
                employee = connection
                    .OpenWithCommand(
                        query,
                        new SqlParameter("@ID", id))
                    .QueryOneOrDefault(creatorFunc);

                if (employee != null)
                {
                    employee.Employees = GetSubEmployees(employee.ID);
                }

                connection.Dispose();
            }

            return JsonConvert.SerializeObject(employee);
        }


        private List<Employee> GetSubEmployees(
            int managerId)
        {
            var query = "SELECT * FROM Employee WHERE ManagerID = @ManagerID";
            Func<SqlDataReader, Employee> creatorFunc = (r) =>
            {
                var employee =  new Employee
                {
                    ID = r.GetInt32(0),
                    Name = r.GetString(1),
                    ManagerID = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                };

                if (employee != null)
                {
                    employee.Employees = GetSubEmployees(employee.ID);
                }

                return employee;
            };
            
            using(var connection = new SqlConnection(ConnectionString))
            {
                return connection
                    .OpenWithCommand(
                        query,
                        new SqlParameter("@ManagerID", managerId))
                    .QueryMany(creatorFunc);
            }
        }*/
    }
    public class Employee
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int? ManagerID { get; set; }
        public List<Employee> Employees { get; set; } = new List<Employee>();
    }

    public static class SqlExtensions
    {
        public static SqlCommand OpenWithCommand(
            this SqlConnection connection,
            string sql,
            params SqlParameter[] parameters)
        {
            if (connection.State == ConnectionState.Closed)
            {
                connection.Open();
            }

            var command = connection.CreateCommand();
            command.CommandText = sql;

            command.Parameters.AddRange(parameters);

            return command;
        }

        public static void Execute(
            this SqlCommand command)
        {
            command?.ExecuteNonQuery();
            command?.Dispose();
        }

        public static TEntity QueryOneOrDefault<TEntity>(
            this SqlCommand command,
            Func<SqlDataReader, TEntity> creator)
        {
            TEntity entity = default;

            using (var reader = command.ExecuteReader()) 
            {
                if (reader.Read())
                {
                    entity = creator(reader);
                }
            }

            command?.Dispose();

            return entity;
        }

        public static List<TEntity> QueryMany<TEntity>(
            this SqlCommand command,
            Func<SqlDataReader, TEntity> creator)
        {
            List<TEntity> entities = new List<TEntity>();

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var entity = creator(reader);
                    entities.Add(entity);
                }
            }

            command?.Dispose();

            return entities;
        }
    }
}