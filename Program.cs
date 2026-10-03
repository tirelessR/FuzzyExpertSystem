using System;
using System.Collections.Generic;
using System.Linq;

namespace FuzzyExpertSystem
{
    // ============================================================
    // Пункт 2. Функция принадлежности — треугольник
    // ============================================================
    class FuzzyTerm
    {
        public string Name { get; }
        public double A { get; }   // левая нога  (μ = 0)
        public double B { get; }   // вершина     (μ = 1)
        public double C { get; }   // правая нога (μ = 0)

        public FuzzyTerm(string name, double a, double b, double c)
        {
            Name = name; A = a; B = b; C = c;
        }

        // Пункт 3. Фазификация: μ(x) — степень принадлежности x данному терму
        public double Mu(double x)
        {
            if (x < A - 1e-9 || x > C + 1e-9) return 0.0;
            if (Math.Abs(x - B) < 1e-9) return 1.0;
            if (x < B) return (B - A < 1e-9) ? 1.0 : (x - A) / (B - A);
            return (C - B < 1e-9) ? 1.0 : (C - x) / (C - B);
        }
    }

    // ============================================================
    // Лингвистическая переменная
    // ============================================================
    class FuzzyVariable
    {
        public string Name { get; }
        public double Min { get; }
        public double Max { get; }
        public List<FuzzyTerm> Terms { get; }

        public FuzzyVariable(string name, double min, double max, List<FuzzyTerm> terms)
        {
            Name = name; Min = min; Max = max; Terms = terms;
        }

        public FuzzyTerm GetTerm(string name) => Terms.First(t => t.Name == name);
    }

    // ============================================================
    // Условие одного правила: имя переменной + имя терма
    // ============================================================
    class RuleCondition
    {
        public string VarName { get; }
        public string TermName { get; }

        public RuleCondition(string varName, string termName)
        {
            VarName = varName;
            TermName = termName;
        }
    }

    // ============================================================
    // Пункт 1. Продукционное правило: ЕСЛИ <условия> ТО <оценка>
    // ============================================================
    class FuzzyRule
    {
        public List<RuleCondition> Conditions { get; }
        public string OutputTerm { get; }

        public FuzzyRule(string outputTerm, params RuleCondition[] conditions)
        {
            OutputTerm = outputTerm;
            Conditions = conditions.ToList();
        }

        public override string ToString()
        {
            string cond = string.Join(" И ", Conditions.Select(c => $"{c.VarName}={c.TermName}"));
            return $"ЕСЛИ {cond} ТО Оценка={OutputTerm}";
        }
    }

    // ============================================================
    // Пункт 3 + 4. Нечёткий логический вывод
    // ============================================================
    class FuzzySystem
    {
        private readonly Dictionary<string, FuzzyVariable> _vars;
        private readonly List<FuzzyRule> _rules;
        private readonly string _outputVarName;
        private const int Steps = 200;   // дискретизация для дефазификации

        public FuzzySystem(
            Dictionary<string, FuzzyVariable> vars,
            List<FuzzyRule> rules,
            string outputVarName)
        {
            _vars = vars;
            _rules = rules;
            _outputVarName = outputVarName;
        }

        // Фазификация: для всех входов вычисляем μ по каждому терму
        public Dictionary<string, Dictionary<string, double>> Fuzzify(
            Dictionary<string, double> inputs)
        {
            var result = new Dictionary<string, Dictionary<string, double>>();
            foreach (var pair in inputs)
            {
                string varName = pair.Key;
                double value = pair.Value;

                var variable = _vars[varName];
                var mus = new Dictionary<string, double>();
                foreach (var term in variable.Terms)
                    mus[term.Name] = term.Mu(value);

                result[varName] = mus;
            }
            return result;
        }

        // Полный цикл: фазификация → агрегация → активизация → аккумуляция → дефазификация
        public double Infer(Dictionary<string, double> inputs, bool verbose = false)
        {
            var mu = Fuzzify(inputs);
            var outVar = _vars[_outputVarName];

            // Агрегированное нечёткое множество на выходе
            var agg = new double[Steps + 1];

            if (verbose)
            {
                Console.WriteLine("  Фазификация:");
                foreach (var pair in mu)
                {
                    string varName = pair.Key;
                    var termMus = pair.Value;

                    var parts = termMus
                        .Where(kv => kv.Value > 1e-6)
                        .Select(kv => $"{kv.Key}={kv.Value:F3}");
                    Console.WriteLine($"    {_vars[varName].Name}: " + string.Join(", ", parts));
                }
                Console.WriteLine("  Сработавшие правила:");
            }

            foreach (var rule in _rules)
            {
                // Агрегация условий правила: оператор "И" = min
                double strength = 1.0;
                foreach (var cond in rule.Conditions)
                    strength = Math.Min(strength, mu[cond.VarName][cond.TermName]);

                if (strength < 1e-6) continue;

                if (verbose)
                    Console.WriteLine($"    {rule}  →  сила = {strength:F3}");

                // Активизация: усечение функции принадлежности выходного терма
                // Аккумуляция: объединение через max
                var outTerm = outVar.GetTerm(rule.OutputTerm);
                for (int i = 0; i <= Steps; i++)
                {
                    double x = outVar.Min + (outVar.Max - outVar.Min) * i / Steps;
                    double clipped = Math.Min(outTerm.Mu(x), strength);
                    if (clipped > agg[i]) agg[i] = clipped;
                }
            }

            // Дефазификация: метод центра тяжести
            double numerator = 0.0, denominator = 0.0;
            for (int i = 0; i <= Steps; i++)
            {
                double x = outVar.Min + (outVar.Max - outVar.Min) * i / Steps;
                numerator += x * agg[i];
                denominator += agg[i];
            }

            double result = denominator > 0 ? numerator / denominator : 0.0;
            if (verbose)
                Console.WriteLine($"  Дефазификация (центр тяжести): {result:F3}");

            return result;
        }
    }

    // ============================================================
    // Пункт 5. Точка входа: сценарии
    // ============================================================
    class Program
    {
        static void Main()
        {
            // --- Входные переменные ---
            var plot = new FuzzyVariable("Сюжет", 0, 10, new List<FuzzyTerm>
            {
                new FuzzyTerm("слабый",  0, 0, 5),
                new FuzzyTerm("обычный", 0, 5, 10),
                new FuzzyTerm("сильный", 5, 10, 10),
            });

            var acting = new FuzzyVariable("Актёры", 0, 10, new List<FuzzyTerm>
            {
                new FuzzyTerm("плохая",  0, 0, 5),
                new FuzzyTerm("средняя", 0, 5, 10),
                new FuzzyTerm("хорошая", 5, 10, 10),
            });

            var fx = new FuzzyVariable("Эффекты", 0, 10, new List<FuzzyTerm>
            {
                new FuzzyTerm("слабые",       0, 0, 5),
                new FuzzyTerm("нормальные",   0, 5, 10),
                new FuzzyTerm("впечатляющие", 5, 10, 10),
            });

            // --- Выходная переменная ---
            var rating = new FuzzyVariable("Оценка", 0, 10, new List<FuzzyTerm>
            {
                new FuzzyTerm("ужасно", 0,   0,   2.5),
                new FuzzyTerm("плохо",  0,   2.5, 5),
                new FuzzyTerm("средне", 2.5, 5,   7.5),
                new FuzzyTerm("хорошо", 5,   7.5, 10),
                new FuzzyTerm("шедевр", 7.5, 10,  10),
            });

            var vars = new Dictionary<string, FuzzyVariable>
            {
                ["Сюжет"]   = plot,
                ["Актёры"]  = acting,
                ["Эффекты"] = fx,
                ["Оценка"]  = rating,
            };

            // --- Пункт 1. База знаний: 18 продукционных правил ---
            var rules = new List<FuzzyRule>
            {
                // Группа 1: всё хорошо
                new FuzzyRule("шедевр",
                    new RuleCondition("Сюжет", "сильный"),
                    new RuleCondition("Актёры", "хорошая"),
                    new RuleCondition("Эффекты", "впечатляющие")),

                new FuzzyRule("шедевр",
                    new RuleCondition("Сюжет", "сильный"),
                    new RuleCondition("Актёры", "хорошая"),
                    new RuleCondition("Эффекты", "нормальные")),

                new FuzzyRule("хорошо",
                    new RuleCondition("Сюжет", "сильный"),
                    new RuleCondition("Актёры", "хорошая"),
                    new RuleCondition("Эффекты", "слабые")),

                // Группа 2: сильный сюжет, средние актёры
                new FuzzyRule("хорошо",
                    new RuleCondition("Сюжет", "сильный"),
                    new RuleCondition("Актёры", "средняя"),
                    new RuleCondition("Эффекты", "впечатляющие")),

                new FuzzyRule("хорошо",
                    new RuleCondition("Сюжет", "сильный"),
                    new RuleCondition("Актёры", "средняя"),
                    new RuleCondition("Эффекты", "нормальные")),

                new FuzzyRule("средне",
                    new RuleCondition("Сюжет", "сильный"),
                    new RuleCondition("Актёры", "средняя"),
                    new RuleCondition("Эффекты", "слабые")),

                new FuzzyRule("средне",
                    new RuleCondition("Сюжет", "сильный"),
                    new RuleCondition("Актёры", "плохая")),

                // Группа 3: обычный сюжет
                new FuzzyRule("хорошо",
                    new RuleCondition("Сюжет", "обычный"),
                    new RuleCondition("Актёры", "хорошая"),
                    new RuleCondition("Эффекты", "впечатляющие")),

                new FuzzyRule("хорошо",
                    new RuleCondition("Сюжет", "обычный"),
                    new RuleCondition("Актёры", "хорошая"),
                    new RuleCondition("Эффекты", "нормальные")),

                new FuzzyRule("средне",
                    new RuleCondition("Сюжет", "обычный"),
                    new RuleCondition("Актёры", "хорошая"),
                    new RuleCondition("Эффекты", "слабые")),

                new FuzzyRule("средне",
                    new RuleCondition("Сюжет", "обычный"),
                    new RuleCondition("Актёры", "средняя")),

                new FuzzyRule("плохо",
                    new RuleCondition("Сюжет", "обычный"),
                    new RuleCondition("Актёры", "плохая")),

                // Группа 4: слабый сюжет
                new FuzzyRule("средне",
                    new RuleCondition("Сюжет", "слабый"),
                    new RuleCondition("Актёры", "хорошая"),
                    new RuleCondition("Эффекты", "впечатляющие")),

                new FuzzyRule("средне",
                    new RuleCondition("Сюжет", "слабый"),
                    new RuleCondition("Актёры", "хорошая"),
                    new RuleCondition("Эффекты", "нормальные")),

                new FuzzyRule("плохо",
                    new RuleCondition("Сюжет", "слабый"),
                    new RuleCondition("Актёры", "средняя")),

                new FuzzyRule("ужасно",
                    new RuleCondition("Сюжет", "слабый"),
                    new RuleCondition("Актёры", "плохая")),

                // Группа 5: эффекты в одиночку не спасают
                new FuzzyRule("средне",
                    new RuleCondition("Сюжет", "сильный"),
                    new RuleCondition("Актёры", "плохая"),
                    new RuleCondition("Эффекты", "впечатляющие")),

                new FuzzyRule("плохо",
                    new RuleCondition("Сюжет", "слабый"),
                    new RuleCondition("Актёры", "плохая"),
                    new RuleCondition("Эффекты", "впечатляющие")),
            };

            var system = new FuzzySystem(vars, rules, "Оценка");

            // ========================================================
            // Пункт 5. Сценарии
            // ========================================================
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("  ЭКСПЕРТНАЯ СИСТЕМА ОЦЕНКИ ФИЛЬМА");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            var scenarios = new List<Scenario>
            {
                new Scenario("Шедевр",         9.5, 9.0, 9.0),
                new Scenario("Артхаус",        9.0, 8.5, 3.0),
                new Scenario("Блокбастер",     5.0, 6.0, 9.5),
                new Scenario("Средний фильм",  5.0, 5.0, 5.0),
                new Scenario("Провал",         2.0, 3.0, 4.0),
            };

            foreach (var s in scenarios)
            {
                Console.WriteLine($"── Сценарий «{s.Name}» ──");
                Console.WriteLine($"  Входы: Сюжет={s.Plot:F1}, Актёры={s.Acting:F1}, Эффекты={s.Fx:F1}");

                var inputs = new Dictionary<string, double>
                {
                    ["Сюжет"]   = s.Plot,
                    ["Актёры"]  = s.Acting,
                    ["Эффекты"] = s.Fx,
                };

                double result = system.Infer(inputs, verbose: true);
                Console.WriteLine($"  ► ИТОГ: Оценка фильма = {result:F2} / 10\n");
            }

            Console.WriteLine("Нажми Enter для выхода...");
            Console.ReadLine();
        }
    }

    // Класс для сценария (вместо кортежа)
    class Scenario
    {
        public string Name { get; }
        public double Plot { get; }
        public double Acting { get; }
        public double Fx { get; }

        public Scenario(string name, double plot, double acting, double fx)
        {
            Name = name; Plot = plot; Acting = acting; Fx = fx;
        }
    }
}