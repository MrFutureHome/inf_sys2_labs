#include <iostream>
#include <fstream>
#include <regex>
#include <string>
#include <cstdlib>;
#include <windows.h>;

void email() {
    std::string email;
    std::cout << "Введите email: ";
    std::cin >> email;
    std::regex email_regex(R"(^\w+([.-]?\w+)@\w+([.-]\w+)$)");

    if (std::regex_match(email, email_regex)) {
        std::cout << "Email корректный." << std::endl;
    }
    else {
        std::cout << "Email некорректный." << std::endl;
    }
}

void phone_numbers() {
    std::ifstream file("phones.txt");
    if (!file) {
        std::cerr << "Не удалось открыть файл phones.txt" << std::endl;
        return;
    }

    std::regex phone_regex(R"(^(8|\+7)\(\d{3}\)\d{3}\-\d{2}\-\d{2}$)");
    std::string line;
    std::cout << "Номера, соответствующие шаблону:\n";
    while (std::getline(file, line)) {
        if (std::regex_match(line, phone_regex)) {
            std::cout << line << std::endl;
        }
    }
    file.close();
}

void CSV() {
    std::ifstream file("data.csv");
    if (!file) {
        std::cerr << "Не удалось открыть файл data.csv" << std::endl;
        return;
    }

    std::regex csv_regex(R"(("[^"]*")|([^",]+))");
    std::string line;

    while (std::getline(file, line)) {
        std::sregex_iterator begin(line.begin(), line.end(), csv_regex);
        std::sregex_iterator end;
        int column_count = 0;
        while (begin != end) {
            std::cout << (*begin).str() << " ";
            ++begin;
            column_count++;
        }
        if (column_count > 0) {
            std::cout << std::endl;
        }
    }
    file.close();
}

void html() {
    std::ifstream file("test.html");
    if (!file) {
        std::cerr << "Не удалось открыть файл document.html" << std::endl;
        return;
    }

    std::regex html_regex(R"(\<(\/)?[\w"=\s:;]+\>)");
    std::string line;
    while (std::getline(file, line)) {
        std::sregex_iterator begin(line.begin(), line.end(), html_regex);
        std::sregex_iterator end;
        while (begin != end) {
            std::cout << (*begin).str() << std::endl;
            ++begin;
        }
    }
    file.close();
}

int main()
{
    SetConsoleCP(1251);
    SetConsoleOutputCP(1251);
    std::cout << "Выберите задание\n";
    std::cout << "1. Проверка правильности введённого email\n2. Проверка правильности номера телефона\n3. Разделение CSV документа\n4. Нахождение тегов HTML\n";
    int check;
    std::cin >> check;

    switch (check) {
    case 1:
        email();
        break;
    case 2:
        phone_numbers();
        break;
    case 3:
        CSV();
        break;
    case 4:
        html();
        break;
    default:
        std::cout << "Неверный выбор." << std::endl;
    }
    return 0;
}


