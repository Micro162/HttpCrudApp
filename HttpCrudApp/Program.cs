
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public record class Person
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public decimal Salary { get; set; }
}

class Program
{
    static List<Person> people = new List<Person>();
    static string url = "http://localhost:8080/";
    static HttpClient client = new HttpClient();

    static async Task Main()
    {
        _ = Task.Run(Server);

        await Task.Delay(500);

        while (true)
        {
            Console.WriteLine("\n--- УПРАВЛЕНИЕ ПОЛЬЗОВАТЕЛЯМИ ---");
            Console.WriteLine("1. Добавить пользователя");
            Console.WriteLine("2. Показать всех пользователей");
            Console.WriteLine("3. Изменить пользователя");
            Console.WriteLine("4. Удалить пользователя");
            Console.WriteLine("0. Выход");
            Console.Write("Выбор: ");

            string choice = Console.ReadLine() ?? "";

            try
            {
                if (choice == "1")
                {
                    Console.Write("Имя: ");
                    string name = Console.ReadLine() ?? "";

                    Console.Write("Дата рождения (гггг-мм-дд): ");
                    DateTime birth = DateTime.Parse(Console.ReadLine()!);

                    Console.Write("Зарплата: ");
                    decimal salary = decimal.Parse(Console.ReadLine()!);

                    var person = new Person
                    {
                        Name = name,
                        BirthDate = birth,
                        Salary = salary
                    };

                    var json = JsonSerializer.Serialize(person);
                    var content = new StringContent(
                        json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url + "people", content);
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                }
                else if (choice == "2")
                {
                    string result = await client.GetStringAsync(url + "people");
                    Console.WriteLine(result);
                }
                else if (choice == "3")
                {
                    Console.Write("ID пользователя: ");
                    string id = Console.ReadLine() ?? "";

                    Console.Write("Новое имя: ");
                    string name = Console.ReadLine() ?? "";

                    Console.Write("Новая зарплата: ");
                    decimal salary = decimal.Parse(Console.ReadLine()!);

                    var person = new Person
                    {
                        Id = id,
                        Name = name,
                        Salary = salary,
                        BirthDate = DateTime.Now
                    };

                    string json = JsonSerializer.Serialize(person);

                    var content = new StringContent(
                        json, Encoding.UTF8, "application/json");

                    var response = await client.PutAsync(
                        url + "people/" + Uri.EscapeDataString(id), content);

                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                }
                else if (choice == "4")
                {
                    Console.Write("ID пользователя: ");
                    string id = Console.ReadLine() ?? "";

                    var response = await client.DeleteAsync(
                        url + "people/" + Uri.EscapeDataString(id));

                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                }
                else if (choice == "0")
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }
        }

        client.Dispose();
    }

    static async Task Server()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(url);

        try
        {
            listener.Start();
            Console.WriteLine("HTTP-сервер запущен: " + url);

            while (true)
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка сервера: " + ex.Message);
        }
    }

    static async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            string path = request.Url!.AbsolutePath.Trim('/');
            string method = request.HttpMethod;

            if (path == "people" && method == "GET")
            {
                await WriteJson(response, people);
            }
            else if (path == "people" && method == "POST")
            {
                using var reader = new System.IO.StreamReader(request.InputStream);
                string body = await reader.ReadToEndAsync();

                var person = JsonSerializer.Deserialize<Person>(body);

                if (person == null)
                {
                    response.StatusCode = 400;
                    await WriteText(response, "Неверные данные");
                    return;
                }

                people.Add(person);
                response.StatusCode = 201;
                await WriteJson(response, person);
            }
            else if (path.StartsWith("people/"))
            {
                string id = Uri.UnescapeDataString(path.Substring("people/".Length));
                var person = people.Find(p => p.Id == id);

                if (person == null)
                {
                    response.StatusCode = 404;
                    await WriteText(response, "Пользователь не найден");
                    return;
                }

                if (method == "PUT")
                {
                    using var reader = new System.IO.StreamReader(request.InputStream);
                    string body = await reader.ReadToEndAsync();

                    var updated = JsonSerializer.Deserialize<Person>(body);

                    if (updated == null)
                    {
                        response.StatusCode = 400;
                        await WriteText(response, "Неверные данные");
                        return;
                    }

                    person.Name = updated.Name;
                    person.BirthDate = updated.BirthDate;
                    person.Salary = updated.Salary;

                    await WriteJson(response, person);
                }
                else if (method == "DELETE")
                {
                    people.Remove(person);
                    await WriteText(response, "Пользователь удалён");
                }
                else
                {
                    response.StatusCode = 405;
                    await WriteText(response, "Метод не поддерживается");
                }
            }
            else
            {
                response.StatusCode = 404;
                await WriteText(response, "Маршрут не найден");
            }
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            await WriteText(response, ex.Message);
        }
        finally
        {
            response.Close();
        }
    }

    static async Task WriteJson(HttpListenerResponse response, object data)
    {
        response.ContentType = "application/json; charset=utf-8";

        string json = JsonSerializer.Serialize(data);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    static async Task WriteText(HttpListenerResponse response, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);

        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength64 = bytes.Length;

        await response.OutputStream.WriteAsync(bytes);
    }
}
