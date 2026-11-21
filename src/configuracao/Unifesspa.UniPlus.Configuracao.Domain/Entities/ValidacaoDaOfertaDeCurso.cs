namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere, sem I/O, a declaração de uma oferta de curso e devolve os campos já resolvidos, ou todos os erros de campo de
/// uma vez. A conferência que depende de outra (turnos contra regime, regime de funcionamento contra regime de turno, base
/// legal contra programa) só roda quando as duas partes foram reconhecidas: do contrário o erro seria derivado de um valor
/// inválido, e não uma incoerência independente.
/// </summary>
internal sealed class ValidacaoDaOfertaDeCurso(DeclaracaoDaOfertaDeCurso declaracao)
{
    private const int EMecCodigoMaxLength = 20;
    private const int CodigoSgaMaxLength = 30;
    private const int BaseLegalMaxLength = 500;
    private const int AtoAutorizacaoMecMaxLength = 300;

    private readonly List<FieldError> _erros = [];
    private CodigoOfertaCurso? _codigo;
    private ProgramaDeOferta? _programa;
    private FormatoPedagogico _formato = FormatoPedagogico.Presencial;
    private bool _funcionamentoReconhecido;
    private RegimeDeFuncionamento _funcionamento = RegimeDeFuncionamento.Nenhum;
    private bool _regimeReconhecido;
    private RegimeDeTurno _regime = RegimeDeTurno.Nenhum;
    private List<TurnoOferta> _turnos = [];
    private bool _turnosResolvidos;

    public Result<CamposDaOfertaDeCurso> Resolver()
    {
        ResolverCodigo();
        ResolverClassificacoes();
        ConferirCombinacoes();
        TextosDaOferta textos = ConferirTextos();

        return Concluir(textos);
    }

    private void ResolverClassificacoes()
    {
        ResolverPrograma();
        ResolverFormato();
        ResolverFuncionamento();
        ResolverRegime();
        ResolverTurnos();
    }

    private void ConferirCombinacoes()
    {
        ConferirCardinalidadeDosTurnos();
        ConferirFuncionamentoComRegime();
        ConferirVagasAnuais();
    }

    private TextosDaOferta ConferirTextos()
    {
        TextosDaOferta textos = new(
            ConferirTamanho(declaracao.EMecCodigo, EMecCodigoMaxLength, "eMecCodigo", OfertaCursoErrorCodes.EMecCodigoTamanho, "Código e-MEC da oferta"),
            ConferirTamanho(declaracao.CodigoSga, CodigoSgaMaxLength, "codigoSga", OfertaCursoErrorCodes.CodigoSgaTamanho, "Código no sistema de gestão acadêmica"),
            ConferirTamanho(declaracao.BaseLegal, BaseLegalMaxLength, "baseLegal", OfertaCursoErrorCodes.BaseLegalTamanho, "Base legal da oferta"),
            ConferirTamanho(declaracao.AtoAutorizacaoMec, AtoAutorizacaoMecMaxLength, "atoAutorizacaoMec", OfertaCursoErrorCodes.AtoAutorizacaoMecTamanho, "Ato de autorização MEC"));
        ConferirBaseLegalDoPrograma(textos.BaseLegal);

        return textos;
    }

    private Result<CamposDaOfertaDeCurso> Concluir(TextosDaOferta textos) =>
        _erros.Count == 0 && _codigo is { } codigo && _programa is { } programa
            ? Result<CamposDaOfertaDeCurso>.Success(new(
                codigo.Valor, programa, _formato, _funcionamento, _regime, TurnosOferta.OrdenarCanonicamente(_turnos),
                textos.EMecCodigo, textos.CodigoSga, declaracao.VagasAnuaisAutorizadas, textos.BaseLegal, textos.AtoAutorizacaoMec))
            : Result<CamposDaOfertaDeCurso>.ValidationFailure(_erros);

    private sealed record TextosDaOferta(string? EMecCodigo, string? CodigoSga, string? BaseLegal, string? AtoAutorizacaoMec);

    private void ResolverCodigo()
    {
        Result<CodigoOfertaCurso> codigo = CodigoOfertaCurso.Criar(declaracao.Codigo);
        if (codigo.IsFailure)
        {
            _erros.Add(new("codigo", codigo.Error!));
            return;
        }

        _codigo = codigo.Value;
    }

    private void ResolverPrograma()
    {
        _programa = CatalogoDe<ProgramaDeOferta>.TentarObter(declaracao.ProgramaDeOferta, out ProgramaDeOferta programa) ? programa : null;
        if (_programa is null)
        {
            AdicionarErro("programaDeOferta", OfertaCursoErrorCodes.ProgramaDeOfertaInvalido, $"Programa de oferta deve ser um de: {CodigosAceitos<ProgramaDeOferta>()}.");
        }
    }

    // O formato é obrigatório, mas ausente vale PRESENCIAL (mesmo expediente do default AMPLA da natureza legal).
    private void ResolverFormato()
    {
        if (declaracao.FormatoPedagogico is null)
        {
            return;
        }

        if (CatalogoDe<FormatoPedagogico>.TentarObter(declaracao.FormatoPedagogico, out FormatoPedagogico formato))
        {
            _formato = formato;
        }
        else
        {
            AdicionarErro("formatoPedagogico", OfertaCursoErrorCodes.FormatoPedagogicoInvalido, $"Formato pedagógico deve ser um de: {CodigosAceitos<FormatoPedagogico>()}.");
        }
    }

    // Ausência e token fora do domínio são erros distintos: quem não informou nada precisa de orientação diferente de quem errou.
    private void ResolverFuncionamento()
    {
        string tokens = string.Join(", ", RegimesDeFuncionamento.TokensCanonicos);
        if (string.IsNullOrWhiteSpace(declaracao.RegimeDeFuncionamento))
        {
            AdicionarErro("regimeDeFuncionamento", OfertaCursoErrorCodes.RegimeDeFuncionamentoObrigatorio, $"Regime de funcionamento é obrigatório e deve ser um de: {tokens}.");
            return;
        }

        _funcionamentoReconhecido = RegimesDeFuncionamento.TryAnalisar(declaracao.RegimeDeFuncionamento, out _funcionamento);
        if (!_funcionamentoReconhecido)
        {
            AdicionarErro("regimeDeFuncionamento", OfertaCursoErrorCodes.RegimeDeFuncionamentoInvalido, $"Regime de funcionamento deve ser um de: {tokens}.");
        }
    }

    private void ResolverRegime()
    {
        string tokens = string.Join(", ", RegimesDeTurno.TokensCanonicos);
        if (string.IsNullOrWhiteSpace(declaracao.RegimeDeTurno))
        {
            AdicionarErro("regimeDeTurno", OfertaCursoErrorCodes.RegimeDeTurnoObrigatorio, $"Regime de turno é obrigatório e deve ser um de: {tokens}.");
            return;
        }

        _regimeReconhecido = RegimesDeTurno.TryAnalisar(declaracao.RegimeDeTurno, out _regime);
        if (!_regimeReconhecido)
        {
            AdicionarErro("regimeDeTurno", OfertaCursoErrorCodes.RegimeDeTurnoInvalido, $"Regime de turno deve ser um de: {tokens}.");
        }
    }

    // Obrigatórios em todo formato pedagógico, a distância inclusive: não há ramo por formato aqui, de propósito.
    private void ResolverTurnos()
    {
        if (declaracao.Turnos is null || declaracao.Turnos.Count == 0)
        {
            AdicionarErroDeTurnos(OfertaCursoErrorCodes.TurnosObrigatorios, "A oferta deve declarar seus turnos — nenhum formato pedagógico dispensa o turno.");
            return;
        }

        ConferirTurnosDeclarados(declaracao.Turnos);
    }

    private void ConferirTurnosDeclarados(IReadOnlyList<string?> declarados)
    {
        List<TurnoOferta> reconhecidos = [.. declarados.Select(ReconhecerTurno).OfType<TurnoOferta>()];
        if (reconhecidos.Count != declarados.Count)
        {
            AdicionarErroDeTurnos(OfertaCursoErrorCodes.TurnoInvalido, $"Turno da oferta deve ser um de: {string.Join(", ", TurnosOferta.TokensCanonicos)}.");
        }
        else if (reconhecidos.Distinct().Count() != reconhecidos.Count)
        {
            AdicionarErroDeTurnos(OfertaCursoErrorCodes.TurnoRepetido, "Os turnos da oferta devem ser distintos entre si.");
        }
        else
        {
            _turnos = reconhecidos;
            _turnosResolvidos = true;
        }
    }

    // O regime é o declarado: dois turnos sob REGULAR é recusa, nunca promoção silenciosa a INTEGRAL.
    private void ConferirCardinalidadeDosTurnos()
    {
        int exigidos = _regimeReconhecido ? RegimesDeTurno.TurnosExigidos(_regime) : 0;
        if (_regimeReconhecido && _turnosResolvidos && _turnos.Count != exigidos)
        {
            AdicionarErroDeTurnos(
                OfertaCursoErrorCodes.CardinalidadeTurnosIncompativelComRegime,
                $"O regime {RegimesDeTurno.ParaTokenCanonico(_regime)} exige exatamente {exigidos} turno(s); foram informados {_turnos.Count}.");
        }
    }

    // INTENSIVO exige regime de turno INTEGRAL; nenhuma dimensão é convertida para acomodar a outra: a recusa é o resultado.
    private void ConferirFuncionamentoComRegime()
    {
        RegimeDeTurno? exigido = _funcionamentoReconhecido ? RegimesDeFuncionamento.RegimeDeTurnoExigido(_funcionamento) : null;
        if (_regimeReconhecido && exigido is not null && _regime != exigido)
        {
            _erros.Add(new("regimeDeFuncionamento", new DomainError(
                OfertaCursoErrorCodes.RegimeDeFuncionamentoIncompativelComRegimeDeTurno,
                $"A oferta {RegimesDeFuncionamento.ParaTokenCanonico(_funcionamento)} exige regime de turno "
                + $"{RegimesDeTurno.ParaTokenCanonico(exigido.Value)}; foi declarado {RegimesDeTurno.ParaTokenCanonico(_regime)}.")));
        }
    }

    private void ConferirVagasAnuais()
    {
        if (declaracao.VagasAnuaisAutorizadas is < 0)
        {
            _erros.Add(new("vagasAnuaisAutorizadas", new DomainError(
                OfertaCursoErrorCodes.VagasAnuaisNegativas,
                "Vagas anuais autorizadas não podem ser negativas (zero é aceito).")));
        }
    }

    private string? ConferirTamanho(string? valor, int maximo, string campo, string codigoDoErro, string rotulo)
    {
        string? normalizado = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        if (normalizado is not null && normalizado.Length > maximo)
        {
            _erros.Add(new(campo, new DomainError(codigoDoErro, $"{rotulo} deve ter no máximo {maximo} caracteres.")));
        }

        return normalizado;
    }

    // Programa fora do Regular exige base legal, também na edição (a transição Regular→Parfor sem base legal falha).
    private void ConferirBaseLegalDoPrograma(string? baseLegal)
    {
        if (_programa is { } programa && programa != ProgramaDeOferta.Regular && baseLegal is null)
        {
            _erros.Add(new("baseLegal", new DomainError(
                OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular,
                "Base legal é obrigatória quando o programa de oferta não é REGULAR.")));
        }
    }

    private void AdicionarErroDeTurnos(string codigo, string mensagem) => AdicionarErro("turnos", codigo, mensagem);

    private void AdicionarErro(string campo, string codigo, string mensagem) =>
        _erros.Add(new(campo, new DomainError(codigo, mensagem)));

    private static string CodigosAceitos<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", CatalogoDe<TEnum>.Todos().Select(static declarado => declarado.Codigo));

    private static TurnoOferta? ReconhecerTurno(string? token) =>
        TurnosOferta.TryAnalisar(token, out TurnoOferta turno) ? turno : null;
}
