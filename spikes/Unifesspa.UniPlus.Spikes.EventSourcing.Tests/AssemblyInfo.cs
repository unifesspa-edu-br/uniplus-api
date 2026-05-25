using Xunit;

// Testes de integração: Testcontainers + múltiplos hosts Wolverine no mesmo processo.
// Serializa a execução para os hosts não disputarem a geração de código do Wolverine
// nem os recursos de containers (alinhado à prática de DisableParallelization do projeto).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
