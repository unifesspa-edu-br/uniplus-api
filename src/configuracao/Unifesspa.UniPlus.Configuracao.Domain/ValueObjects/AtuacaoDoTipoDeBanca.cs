namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Como as bancas de um tipo atuam no processo seletivo, já conferido: o papel, as duas marcas e os tipos de decisão que o
/// tipo julga. Só <see cref="Validar"/> cria uma atuação. As mensagens dizem o que é aceito pelos rótulos e nunca repetem o
/// texto recebido.
/// </summary>
public sealed record AtuacaoDoTipoDeBanca
{
    private const string CampoDoPapel = "papel";
    private const string CampoDosTipos = "tiposDeDecisaoJulgados";
    private const string MensagemDaIncoerencia =
        "O tipo de banca que avalia o mérito declara ao menos um tipo de decisão que julga; os tipos com outro papel não declaram nenhum.";

    private readonly List<TipoMotivoDeDecisao> _tiposDeDecisaoJulgados = [];

    // Leitura pelo EF Core, que preenche as propriedades e o campo da lista.
    private AtuacaoDoTipoDeBanca()
    {
    }

    private AtuacaoDoTipoDeBanca(
        PapelDaBanca papel,
        bool exigeDuasOuMaisBancas,
        bool encerraRecursoNaPrimeiraInstancia,
        IEnumerable<TipoMotivoDeDecisao> tiposDeDecisaoJulgados)
    {
        Papel = papel;
        ExigeDuasOuMaisBancas = exigeDuasOuMaisBancas;
        EncerraRecursoNaPrimeiraInstancia = encerraRecursoNaPrimeiraInstancia;
        _tiposDeDecisaoJulgados.AddRange(tiposDeDecisaoJulgados);
    }

    /// <summary>O papel do tipo.</summary>
    public PapelDaBanca Papel { get; }

    /// <summary>O tipo só vale com duas ou mais bancas no mesmo lugar.</summary>
    public bool ExigeDuasOuMaisBancas { get; }

    /// <summary>O recurso do que o tipo avalia termina na primeira instância.</summary>
    public bool EncerraRecursoNaPrimeiraInstancia { get; }

    /// <summary>Os tipos de decisão que o tipo julga, em ordem alfabética ordinal do código e sem repetição.</summary>
    public IReadOnlyList<TipoMotivoDeDecisao> TiposDeDecisaoJulgados => _tiposDeDecisaoJulgados.AsReadOnly();

    /// <inheritdoc />
    public bool Equals(AtuacaoDoTipoDeBanca? outra) =>
        outra is not null
        && Papel == outra.Papel
        && ExigeDuasOuMaisBancas == outra.ExigeDuasOuMaisBancas
        && EncerraRecursoNaPrimeiraInstancia == outra.EncerraRecursoNaPrimeiraInstancia
        && _tiposDeDecisaoJulgados.SequenceEqual(outra._tiposDeDecisaoJulgados);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode codigo = new();
        codigo.Add(Papel);
        codigo.Add(ExigeDuasOuMaisBancas);
        codigo.Add(EncerraRecursoNaPrimeiraInstancia);
        _tiposDeDecisaoJulgados.ForEach(tipo => codigo.Add(tipo));
        return codigo.ToHashCode();
    }

    /// <summary>
    /// Confere o papel e os tipos de decisão como chegaram e acumula todas as recusas, sem alterar nada. Só o papel que
    /// avalia o mérito declara tipos de decisão, e declara ao menos um; o elemento nulo, em branco ou fora do vocabulário é
    /// recusado por posição, e o tipo repetido, a partir da segunda ocorrência. Lista ausente vale como lista vazia.
    /// </summary>
    public static Result<AtuacaoDoTipoDeBanca> Validar(DeclaracaoDoTipoDeBanca declaracao)
    {
        ArgumentNullException.ThrowIfNull(declaracao);

        IReadOnlyList<string?> recebidos = declaracao.TiposDeDecisaoJulgados ?? [];
        List<FieldError> erros = [];
        PapelDaBanca? papel = LerPapel(declaracao.Papel, erros);
        erros.AddRange(ErrosDosTipos(recebidos));
        erros.AddRange(ErrosDaCoerencia(papel, recebidos.Count));

        return erros.Count > 0
            ? Result<AtuacaoDoTipoDeBanca>.ValidationFailure(erros)
            : Result<AtuacaoDoTipoDeBanca>.Success(Conferida(declaracao, papel!.Value, recebidos));
    }

    private static AtuacaoDoTipoDeBanca Conferida(DeclaracaoDoTipoDeBanca declaracao, PapelDaBanca papel, IReadOnlyList<string?> recebidos) =>
        new(
            papel,
            declaracao.ExigeDuasOuMaisBancas,
            declaracao.EncerraRecursoNaPrimeiraInstancia,
            Reconhecidos(recebidos).OrderBy(static tipo => tipo.Codigo(), StringComparer.Ordinal));

    private static PapelDaBanca? LerPapel(string? codigo, List<FieldError> erros)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            erros.Add(Recusa(CampoDoPapel, TipoBancaErrorCodes.PapelObrigatorio, "Informe o papel do tipo de banca."));
            return null;
        }

        if (CatalogoDe<PapelDaBanca>.TentarObter(codigo, out PapelDaBanca papel))
        {
            return papel;
        }

        erros.Add(Recusa(CampoDoPapel, TipoBancaErrorCodes.PapelForaDoVocabulario, MensagemDoPapelForaDoVocabulario()));
        return null;
    }

    private static IEnumerable<FieldError> ErrosDosTipos(IReadOnlyList<string?> recebidos)
    {
        HashSet<string> jaLidos = new(StringComparer.Ordinal);
        for (int posicao = 0; posicao < recebidos.Count; posicao++)
        {
            FieldError? erro = ErroDoTipo(recebidos[posicao], posicao, jaLidos);
            if (erro is not null)
            {
                yield return erro;
            }
        }
    }

    // Só o código que já passou como tipo válido entra em `jaLidos`; a repetição se decide pelo texto.
    private static FieldError? ErroDoTipo(string? codigo, int posicao, HashSet<string> jaLidos)
    {
        string campo = $"{CampoDosTipos}[{posicao}]";
        if (!CatalogoDe<TipoMotivoDeDecisao>.TentarObter(codigo, out TipoMotivoDeDecisao tipo))
        {
            return Recusa(campo, TipoBancaErrorCodes.TipoDeDecisaoForaDoVocabulario, MensagemDoTipoForaDoVocabulario());
        }

        return jaLidos.Add(codigo!)
            ? null
            : Recusa(campo, TipoBancaErrorCodes.TipoDeDecisaoRepetido, MensagemDoTipoRepetido(tipo));
    }

    private static IEnumerable<FieldError> ErrosDaCoerencia(PapelDaBanca? papel, int tiposRecebidos)
    {
        if (papel is { } lido && !CoerenteComOPapel(lido, tiposRecebidos))
        {
            yield return Recusa(CampoDosTipos, TipoBancaErrorCodes.TiposDeDecisaoIncoerentesComOPapel, MensagemDaIncoerencia);
        }
    }

    private static IEnumerable<TipoMotivoDeDecisao> Reconhecidos(IReadOnlyList<string?> recebidos)
    {
        foreach (string? codigo in recebidos.Distinct())
        {
            if (CatalogoDe<TipoMotivoDeDecisao>.TentarObter(codigo, out TipoMotivoDeDecisao tipo))
            {
                yield return tipo;
            }
        }
    }

    private static bool CoerenteComOPapel(PapelDaBanca papel, int tiposRecebidos) =>
        papel == PapelDaBanca.AvaliaOMerito ? tiposRecebidos > 0 : tiposRecebidos == 0;

    private static string MensagemDoPapelForaDoVocabulario() =>
        $"O papel informado não é um dos aceitos. Papéis aceitos: {RotulosAceitos<PapelDaBanca>()}.";

    private static string MensagemDoTipoForaDoVocabulario() =>
        $"Um dos tipos de decisão informados não é um dos aceitos. Tipos de decisão aceitos: {RotulosAceitos<TipoMotivoDeDecisao>()}.";

    private static string MensagemDoTipoRepetido(TipoMotivoDeDecisao tipo) =>
        $"O tipo de decisão '{tipo.Rotulo()}' aparece mais de uma vez entre os que o tipo de banca julga.";

    private static string RotulosAceitos<TEnum>()
        where TEnum : struct, Enum =>
        string.Join("; ", CatalogoDe<TEnum>.Todos().Select(static declarado => declarado.Rotulo));

    private static FieldError Recusa(string campo, string codigoDoErro, string mensagem) =>
        new(campo, new DomainError(codigoDoErro, mensagem));
}
