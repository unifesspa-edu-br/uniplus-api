namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>
/// Monta o laudo médico complementar (tipo de documento) ou o fato "tem renda" no circuito de aprovação, solicitado por
/// Isabella e, quando o cenário pede, com as manifestações técnicas de Kevin e de Lúcia, Encarregados de dados.
/// </summary>
internal sealed class ItemSobAprovacaoBuilder
{
    public static readonly AtorDoAto Kevin = new(Emissor, "kevin.carvalho", "Kevin Anderson Carvalho");
    public static readonly AtorDoAto Lucia = new(Emissor, "lucia.almeida", "Lúcia Bruna Maya Almeida");

    private readonly List<(string Parecer, string? Texto, AtorDoAto Autor)> _manifestacoes = [];
    private bool _solicitado;

    /// <summary>As manifestações registradas no ciclo do item, na ordem em que o cenário as pediu.</summary>
    public List<ManifestacaoDaProtecaoDoCatalogo> Eventos { get; } = [];

    public ItemSobAprovacaoBuilder Solicitado()
    {
        _solicitado = true;
        return this;
    }

    public ItemSobAprovacaoBuilder ComManifestacao(string parecer, string? texto = "A classificação deve ser sensível.", AtorDoAto? autor = null)
    {
        _manifestacoes.Add((parecer, texto, autor ?? Kevin));
        return this;
    }

    public TipoDocumento Tipo()
    {
        TipoDocumento tipo = NovoTipo();
        Preparar(tipo.SolicitarAprovacaoDaProtecao, tipo.Aprovacao, new ItemDaProtecao(CadastroDoCatalogo.TipoDocumento, tipo.Id));
        return tipo;
    }

    public FatoCandidato Fato()
    {
        FatoCandidato fato = NovoFato();
        Preparar(fato.SolicitarAprovacaoDaProtecao, fato.Aprovacao, new ItemDaProtecao(CadastroDoCatalogo.Fato, fato.Id));
        return fato;
    }

    private void Preparar(Func<string, DateTimeOffset, Result> solicitar, AprovacaoDaProtecao circuito, ItemDaProtecao item)
    {
        if (_solicitado)
        {
            solicitar(Isabella, Agora.AddHours(-2));
        }

        foreach ((string parecer, string? texto, AtorDoAto autor) in _manifestacoes)
        {
            Eventos.Add(ManifestacoesDoCiclo.De(item, circuito.Ciclo, Eventos)
                .Registrar(circuito, circuito.Ciclo, Conteudo(parecer, texto), autor, Agora.AddMinutes(-30 + Eventos.Count)).Value!);
        }
    }

    private static ConteudoDaManifestacao Conteudo(string parecer, string? texto)
    {
        List<FieldError> erros = [];
        return ConteudoDaManifestacao.Ler(parecer, texto, erros)
            ?? throw new InvalidOperationException(string.Join("; ", erros.Select(static e => e.Error.Message)));
    }
}
