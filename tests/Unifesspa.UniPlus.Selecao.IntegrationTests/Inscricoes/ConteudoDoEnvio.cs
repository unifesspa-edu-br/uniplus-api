namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que o candidato declara e o que a avaliação apura no envio da inscrição. Cada propriedade parte do envio mais
/// simples (sem respostas, sem derivados, nada a descartar, uma só opção de curso); o teste muda só o que o assunto dele pede.
/// </summary>
internal sealed record ConteudoDoEnvio
{
    public Dictionary<string, JsonElement> Respostas { get; init; } = CenarioDaInscricaoGravada.Respostas();

    public Dictionary<string, JsonElement> Derivados { get; init; } = CenarioDaInscricaoGravada.Respostas();

    public ResultadoDaAvaliacaoDaInscricao Avaliacao { get; init; } = FormularioDeTeste.AvaliacaoSemOcultos;

    public RespostasAdmitidas Admitidas { get; init; } = CenarioDaInscricaoGravada.Admitidas;

    public Guid? SegundaOfertaDeCurso { get; init; }

    public DateTimeOffset Instante { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<ArquivoConfirmado> Arquivos { get; init; } = [];
}
