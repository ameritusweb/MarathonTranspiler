using MarathonTranspiler.Core;
using MarathonTranspiler.Transpilers.Orleans;

namespace MarathonTranspiler.Test
{
    [TestFixture]
    public class OrleansTranspilerTests
    {
        private OrleansTranspiler _transpiler;
        private List<AnnotatedCode> _annotatedCode;

        [SetUp]
        public void Setup()
        {
            _transpiler = new OrleansTranspiler(new OrleansConfig
            {
                Stateful = false,
                GrainKeyTypes = new Dictionary<string, string>
                {
                    { "OrderGrain", "string" }
                },
                Streams = new Dictionary<string, List<string>>()
            });
            _annotatedCode = new List<AnnotatedCode>();
        }

        [Test]
        public void BasicGrainGeneration_ShouldCreateGrainAndInterface()
        {
            // Arrange
            var code = new AnnotatedCode
            {
                Annotations = new List<Annotation>
                {
                    new Annotation
                    {
                        Name = "varInit",
                        Values = new List<KeyValuePair<string, string>>
                        {
                            new("className", "OrderGrain"),
                            new("type", "string")
                        }
                    }
                },
                Code = new List<string> { "this.OrderId = \"\";" }
            };
            _annotatedCode.Add(code);

            // Act
            _transpiler.ProcessAnnotatedCode(_annotatedCode);
            var output = _transpiler.GenerateOutput();

            // Assert
            StringAssert.Contains("public interface IOrderGrain : IGrainWithStringKey", output);
            StringAssert.Contains("public class OrderGrain : Grain, IOrderGrain", output);
            StringAssert.Contains("public string OrderId { get; set; }", output);
        }

        [Test]
        public void StatefulGrain_ShouldGenerateStateManagement()
        {
            // Arrange
            _transpiler = new OrleansTranspiler(new OrleansConfig
            {
                Stateful = true,
                GrainKeyTypes = new Dictionary<string, string>
        {
            { "OrderGrain", "string" }
        }
            });

            var initCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
        {
            new Annotation
            {
                Name = "varInit",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("className", "OrderGrain"),
                    new("type", "string")
                }
            }
        },
                Code = new List<string> { "this.OrderId = \"\";" }
            };

            var methodCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
        {
            new Annotation
            {
                Name = "run",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("className", "OrderGrain"),
                    new("functionName", "SetOrderId")
                }
            }
        },
                Code = new List<string> { "this.OrderId = \"123\";" }
            };

            _annotatedCode.Add(initCode);
            _annotatedCode.Add(methodCode);

            // Act
            _transpiler.ProcessAnnotatedCode(_annotatedCode);
            var output = _transpiler.GenerateOutput();

            // Assert
            StringAssert.Contains("private IPersistentState<OrderGrainState>", output);
            StringAssert.Contains("[PersistentState", output);
            StringAssert.Contains("public class OrderGrainState", output);
            StringAssert.Contains("await _state.WriteStateAsync();", output);
        }

        [Test]
        public void GrainWithStreams_ShouldGenerateStreamInterfaces()
        {
            // Arrange
            _transpiler = new OrleansTranspiler(new OrleansConfig
            {
                GrainKeyTypes = new Dictionary<string, string>
                {
                    { "OrderGrain", "string" }
                },
                Streams = new Dictionary<string, List<string>>
                {
                    { "OrderGrain", new List<string> { "IAsyncStream<OrderEvent>" } }
                }
            });

            var code = new AnnotatedCode
            {
                Annotations = new List<Annotation>
                {
                    new Annotation
                    {
                        Name = "varInit",
                        Values = new List<KeyValuePair<string, string>>
                        {
                            new("className", "OrderGrain"),
                            new("type", "string")
                        }
                    }
                },
                Code = new List<string> { "this.OrderId = \"\";" }
            };
            _annotatedCode.Add(code);

            // Act
            _transpiler.ProcessAnnotatedCode(_annotatedCode);
            var output = _transpiler.GenerateOutput();

            // Assert
            StringAssert.Contains("private IAsyncStream<OrderEvent> _stream;", output);
        }

        [Test]
        public void GrainMethod_ShouldGenerateTaskMethod()
        {
            // Arrange
            var initCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
                {
                    new Annotation
                    {
                        Name = "varInit",
                        Values = new List<KeyValuePair<string, string>>
                        {
                            new("className", "OrderGrain"),
                            new("type", "decimal")
                        }
                    }
                },
                Code = new List<string> { "this.Total = 0m;" }
            };

            var methodCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
                {
                    new Annotation
                    {
                        Name = "run",
                        Values = new List<KeyValuePair<string, string>>
                        {
                            new("className", "OrderGrain"),
                            new("functionName", "AddItem")
                        }
                    },
                    new Annotation
                    {
                        Name = "parameter",
                        Values = new List<KeyValuePair<string, string>>
                        {
                            new("name", "price"),
                            new("type", "decimal"),
                            new("value", "10.0m")
                        }
                    }
                },
                Code = new List<string> { "this.Total += price;" }
            };

            _annotatedCode.Add(initCode);
            _annotatedCode.Add(methodCode);

            // Act
            _transpiler.ProcessAnnotatedCode(_annotatedCode);
            var output = _transpiler.GenerateOutput();

            // Assert
            StringAssert.Contains("Task AddItem(decimal price);", output);
            StringAssert.Contains("public async Task AddItem(decimal price)", output);
            StringAssert.Contains("this.Total += price;", output);
        }

        [Test]
        public void GrainWithGuidKey_ShouldUseCorrectInterface()
        {
            // Arrange
            _transpiler = new OrleansTranspiler(new OrleansConfig
            {
                GrainKeyTypes = new Dictionary<string, string>
        {
            { "UserGrain", "guid" }
        }
            });

            var code = new AnnotatedCode
            {
                Annotations = new List<Annotation>
        {
            new Annotation
            {
                Name = "varInit",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("className", "UserGrain"),
                    new("type", "Guid")
                }
            }
        },
                Code = new List<string> { "this.UserId = Guid.Empty;" }
            };
            _annotatedCode.Add(code);

            // Act
            _transpiler.ProcessAnnotatedCode(_annotatedCode);
            var output = _transpiler.GenerateOutput();

            // Assert
            StringAssert.Contains("public interface IUserGrain : IGrainWithGuidKey", output);
        }

        [Test]
        public void GrainWithStreamConsumer_ShouldImplementStreamHandling()
        {
            // Arrange
            _transpiler = new OrleansTranspiler(new OrleansConfig
            {
                GrainKeyTypes = new Dictionary<string, string>
        {
            { "OrderGrain", "string" }
        },
                Streams = new Dictionary<string, List<string>>
        {
            { "OrderGrain", new List<string> { "IAsyncStream<OrderEvent>" } }
        }
            });

            var initCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
        {
            new Annotation
            {
                Name = "varInit",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("className", "OrderGrain"),
                    new("type", "string")
                }
            }
        },
                Code = new List<string> { "this.OrderId = \"\";" }
            };

            var consumeCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
        {
            new Annotation
            {
                Name = "run",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("className", "OrderGrain"),
                    new("functionName", "OnNextAsync")
                }
            },
            new Annotation
            {
                Name = "parameter",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("name", "orderEvent"),
                    new("type", "OrderEvent")
                }
            }
        },
                Code = new List<string> { "await _stream.OnNextAsync(orderEvent);" }
            };

            _annotatedCode.Add(initCode);
            _annotatedCode.Add(consumeCode);

            // Act
            _transpiler.ProcessAnnotatedCode(_annotatedCode);
            var output = _transpiler.GenerateOutput();

            // Assert
            StringAssert.Contains("private IAsyncStream<OrderEvent> _stream;", output);
            StringAssert.Contains("public async Task OnNextAsync(OrderEvent orderEvent)", output);
        }

        [Test]
        public void StatefulGrainWithTimers_ShouldImplementTimerCallback()
        {
            // Arrange
            _transpiler = new OrleansTranspiler(new OrleansConfig
            {
                Stateful = true,
                GrainKeyTypes = new Dictionary<string, string>
        {
            { "OrderGrain", "string" }
        }
            });

            var initCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
        {
            new Annotation
            {
                Name = "varInit",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("className", "OrderGrain"),
                    new("type", "IDisposable")
                }
            }
        },
                Code = new List<string> { "this.Timer = null;" }
            };

            var timerCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
        {
            new Annotation
            {
                Name = "run",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("className", "OrderGrain"),
                    new("functionName", "StartTimer")
                }
            }
        },
                Code = new List<string>
        {
            "this.Timer = RegisterTimer(CheckOrderStatus, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));"
        }
            };

            _annotatedCode.Add(initCode);
            _annotatedCode.Add(timerCode);

            // Act
            _transpiler.ProcessAnnotatedCode(_annotatedCode);
            var output = _transpiler.GenerateOutput();

            // Assert
            StringAssert.Contains("public IDisposable Timer { get; set; }", output);
            StringAssert.Contains("RegisterTimer", output);
        }

        [Test]
        public void OrleansGrain_WithAssertions_ShouldGenerateTests()
        {
            // Arrange
            _transpiler = new OrleansTranspiler(new OrleansConfig
            {
                GrainKeyTypes = new Dictionary<string, string>
        {
            { "CounterGrain", "string" }
        }
            });

            var initCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
        {
            new Annotation
            {
                Name = "varInit",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("className", "CounterGrain"),
                    new("type", "int")
                }
            }
        },
                Code = new List<string> { "this.Count = 0;" }
            };

            var assertCode = new AnnotatedCode
            {
                Annotations = new List<Annotation>
        {
            new Annotation
            {
                Name = "assert",
                Values = new List<KeyValuePair<string, string>>
                {
                    new("className", "CounterGrain"),
                    new("condition", "this.Count == 0")
                }
            }
        },
                Code = new List<string> { "Counter should start at zero" }
            };

            _annotatedCode.Add(initCode);
            _annotatedCode.Add(assertCode);

            // Act
            _transpiler.ProcessAnnotatedCode(_annotatedCode);
            var output = _transpiler.GenerateOutput();

            // Assert
            StringAssert.Contains("public class CounterGrainTests", output);
            StringAssert.Contains("[Fact]", output);
            StringAssert.Contains("Assert.True(this.Count == 0, \"Counter should start at zero\");", output);
        }
    }
}
