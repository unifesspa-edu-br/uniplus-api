namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração de distribuição de vagas de uma oferta de curso dentro do
/// <see cref="ProcessoSeletivo"/> (Story #773): os inputs que o
/// admin declara — <see cref="VoBase"/>, <see cref="Pr"/>, a referência à
/// regra de distribuição tipada (<c>rol_de_regras</c>) e, quando aplicável, o
/// snapshot da referência demográfica — mais o subconjunto de
/// <see cref="ModalidadeSelecionada"/> que participa desta oferta.
/// </summary>
/// <remarks>
/// <para>
/// O <c>QuadroDeVagas</c> (quantidade de vagas por modalidade,
/// <see cref="VagasOfertadas"/>) é OUTPUT DERIVADO — resultado de aplicar a
/// regra de distribuição sobre os inputs desta configuração — materializado
/// por <see cref="Criar"/> na mesma operação (issue #848/ADR-0115), via
/// <see cref="Services.CalculadoraQuadroVagasLei12711"/> no ramo federal ou
/// fixado pelo edital no ramo institucional.
/// </para>
/// <para>
/// Entidade interna do agregado <see cref="ProcessoSeletivo"/>: criada,
/// substituída e persistida exclusivamente pela raiz, via
/// <see cref="ProcessoSeletivo.DefinirDistribuicaoVagas"/>.
/// </para>
/// </remarks>
public sealed class ConfiguracaoDistribuicaoVagas : EntityBase
{
    private const decimal PrMinimo = 0.5m;
    private const decimal PrMaximo = 1m;

    public Guid ProcessoSeletivoId { get; private set; }
    public Guid OfertaCursoOrigemId { get; private set; }
    public int VoBase { get; private set; }
    public decimal Pr { get; private set; }
    public ReferenciaRegra RegraDistribuicao { get; private set; } = null!;

    /// <summary>
    /// Snapshot da <c>ReferenciaReservaDemografica</c> (Censo + percentuais) —
    /// obrigatório quando <see cref="RegraDistribuicao"/> é do ramo federal
    /// (<see cref="RegraDistribuicaoVagasCodigo.EhRamoFederal"/>, INV-5); ausente no
    /// quadro fixo (sem cálculo por percentual).
    /// </summary>
    public ReferenciaReservaDemograficaSnapshot? ReferenciaDemografica { get; private set; }

    /// <summary>
    /// Regra que rege a capagem em <c>VO</c> e a prioridade legal entre
    /// sub-reservas na escassez (<c>RECONCILIACAO-VAGAS-ART11-PU</c>) —
    /// obrigatória no ramo federal, opcional no institucional (quadro fixo não
    /// reconcilia). Só a referência é congelada aqui (ADR-0115); os motores de
    /// ajuste não-federais que o cadastro da regra também descreve não são
    /// executados por esta story.
    /// </summary>
    public ReferenciaRegra? RegraAjuste { get; private set; }

    public int VrNominal { get; private set; }
    public int VrFinal { get; private set; }
    public int Estouro { get; private set; }
    public bool CapadoEmVo { get; private set; }
    public int TotalPublicado { get; private set; }

    private readonly List<ModalidadeSelecionada> _modalidades = [];
    public IReadOnlyCollection<ModalidadeSelecionada> Modalidades => _modalidades.AsReadOnly();

    private readonly List<VagaOfertada> _vagasOfertadas = [];

    /// <summary>
    /// O quadro de vagas — output derivado, materializado dentro desta mesma
    /// factory (ADR-0115), nunca por comando separado.
    /// </summary>
    public IReadOnlyCollection<VagaOfertada> VagasOfertadas => _vagasOfertadas.AsReadOnly();

    private ConfiguracaoDistribuicaoVagas() { }

    /// <summary>
    /// Acumula (ADR-0125) as três únicas checagens que não dependem de nenhuma modalidade já
    /// RESOLVIDA no cadastro — <paramref name="voBase"/>/<paramref name="pr"/> são primitivos
    /// do payload, e a contagem de modalidades já é conhecida pelos Ids crus do request, antes
    /// de qualquer <c>IModalidadeReader</c>. Sem incluir a contagem aqui, um payload com
    /// <c>ModalidadeIds</c> vazio E uma oferta/regra inexistente devolveria o erro de
    /// catálogo em vez de <c>ModalidadesVazias</c> — a checagem de forma perderia para o I/O
    /// exatamente no caso em que não precisava dele. Diferente das demais invariantes de
    /// <see cref="Criar"/> (que dependem do <em>Código</em> de cada
    /// <see cref="ModalidadeSelecionada"/> já resolvida, não apenas da contagem), esta é
    /// exposta para o handler confirmar a forma de TODAS as distribuições do payload numa
    /// primeira passada, antes de resolver oferta/modalidades/regras nos cadastros vivos
    /// (mesmo padrão de <c>EtapaProcesso.ValidarFormaBasica</c>, PR #1218).
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(int voBase, decimal pr, int quantidadeModalidades)
    {
        List<FieldError> erros = [];

        if (voBase <= 0)
        {
            erros.Add(new("voBase", new DomainError(
                "ConfiguracaoDistribuicaoVagas.VoBaseInvalido", "VO_base deve ser maior que zero.")));
        }

        // INV-1: 0,5 ≤ PR ≤ 1 (art. 10, II — piso legal 50%, teto 100%).
        if (pr < PrMinimo || pr > PrMaximo)
        {
            erros.Add(new("pr", new DomainError(
                "ConfiguracaoDistribuicaoVagas.PrForaDoLimite",
                $"PR deve estar entre {PrMinimo:0.0} e {PrMaximo:0.0} (art. 10, II).")));
        }

        if (quantidadeModalidades == 0)
        {
            erros.Add(new("modalidades", new DomainError(
                "ConfiguracaoDistribuicaoVagas.ModalidadesVazias",
                "A oferta deve ter ao menos uma modalidade selecionada.")));
        }

        return erros;
    }

    /// <summary>
    /// Cria a configuração de distribuição de vagas de uma oferta, validando
    /// INV-1 (limites do PR), INV-5 (referência demográfica completa quando
    /// ramo federal) e INV-6 (8 modalidades federais + AC obrigatórias quando
    /// ramo federal). As invariantes próprias de cada modalidade (INV-2, INV-12,
    /// coerência de composição/remanejamento) já foram validadas em
    /// <see cref="ModalidadeSelecionada.Criar"/>.
    /// </summary>
    /// <param name="argsAjuste">
    /// Motor que reconcilia o quadro fixo cuja soma passa do <paramref name="voBase"/>.
    /// <see langword="null"/> deixa a recusa valer: sem motor declarado não há de onde tirar.
    /// </param>
    /// <param name="modalidadesAdmitidas">
    /// Códigos que a regra de distribuição reconhece, declarados no catálogo e resolvidos
    /// pela camada de aplicação. <see langword="null"/> quando a regra não restringe o rol.
    /// </param>
    /// <param name="vagasAnuaisAutorizadas">
    /// Teto de vagas anuais autorizadas no e-MEC para a oferta, resolvido pela camada de
    /// aplicação. <see langword="null"/> quando a oferta não o declara — ausência não é
    /// permissão nem proibição, e nenhum teto é imposto.
    /// </param>
    public static Result<ConfiguracaoDistribuicaoVagas> Criar(
        Guid ofertaCursoOrigemId,
        int voBase,
        decimal pr,
        ReferenciaRegra regraDistribuicao,
        ReferenciaRegra? regraAjuste,
        ReferenciaReservaDemograficaSnapshot? referenciaDemografica,
        IReadOnlyList<ModalidadeSelecionada> modalidades,
        int? vagasAnuaisAutorizadas = null,
        IReadOnlyCollection<string>? modalidadesAdmitidas = null,
        ArgsRegraAjusteDistribuicao? argsAjuste = null)
    {
        ArgumentNullException.ThrowIfNull(regraDistribuicao);
        ArgumentNullException.ThrowIfNull(modalidades);

        // Acumula (ADR-0125) as checagens independentes entre si — VoBase/PR (formais,
        // primitivas) e as de coerência do conjunto de modalidades já resolvido. A partir
        // daqui (ramo Lei 12.711 × Institucional, montagem do quadro) as checagens são
        // sequenciais e dependentes umas das outras — ex.: só faz sentido avaliar
        // completude das modalidades federais SE a regra for a Lei 12.711, e a montagem do
        // quadro só roda se tudo acima passou — então permanecem retorno na primeira falha,
        // mesma decisão de escopo de CriterioDesempate (PR #1216) para checks
        // dependentes de dado já resolvido.
        List<FieldError> erros = ValidarFormaBasica(voBase, pr, modalidades.Count);

        // O teto é anual e o VO é de um certame: uma oferta de 40 vagas comporta dois
        // processos de 20. Comparar contra o VO de um certame é o limite superior seguro —
        // nunca recusa caso legítimo — e não alcança dois certames que somados ultrapassem
        // o ano, o que exigiria conhecer os demais certames da oferta.
        if (vagasAnuaisAutorizadas is int teto && voBase > teto)
        {
            erros.Add(new("voBase", new DomainError(
                "ConfiguracaoDistribuicaoVagas.VoBaseAcimaDasVagasAutorizadas",
                $"O VO_base ({voBase}) excede as {teto} vagas anuais autorizadas para a oferta.")));
        }

        List<string> codigosInformados = [.. modalidades.Select(m => m.Codigo)];

        // A regra declara o conjunto exato que reconhece — não um teto. Um certame
        // exclusivo de vagas por acréscimo não oferta ampla concorrência (rejeita extra),
        // e a distribuição pelo art. 10 não fecha o quadro com uma modalidade da Lei
        // faltando (rejeita ausência) — ambos os lados do rol fechado são a mesma
        // invariante, só o sentido da comparação muda. Sem a metade "faltantes", a Lei
        // 12.711 com AC_PCD aceitaria o rol básico de 9 sem AC_PCD, e PSIQ aceitaria só
        // AC_I sem AC_Q, produzindo um quadro que não corresponde ao rol declarado.
        if (modalidadesAdmitidas is { Count: > 0 })
        {
            string[] naoAdmitidas = [.. codigosInformados
                .Where(codigo => !modalidadesAdmitidas.Contains(codigo, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)];

            if (naoAdmitidas.Length > 0)
            {
                erros.Add(new("modalidades", new DomainError(
                    "ConfiguracaoDistribuicaoVagas.ModalidadeNaoAdmitidaPelaRegra",
                    $"A regra {regraDistribuicao.Codigo} não admite a(s) modalidade(s) {string.Join(", ", naoAdmitidas)}.")));
            }

            string[] faltantesDoRol = [.. modalidadesAdmitidas
                .Where(codigo => !codigosInformados.Contains(codigo, StringComparer.Ordinal))];

            if (faltantesDoRol.Length > 0)
            {
                erros.Add(new("modalidades", new DomainError(
                    "ConfiguracaoDistribuicaoVagas.ModalidadeDoRolAusente",
                    $"A regra {regraDistribuicao.Codigo} exige a(s) modalidade(s) {string.Join(", ", faltantesDoRol)}.")));
            }
        }

        // Os motores são de distribuição institucional. No ramo federal a reconciliação é a
        // da calculadora — cap no VO e prioridade da reserva de baixa renda, art. 11 §único —,
        // e aceitar args aqui os deixaria inertes: o operador declararia um ajuste que nunca
        // roda, e o quadro sairia diferente do que ele configurou sem nada dizer.
        if (argsAjuste is not null && RegraDistribuicaoVagasCodigo.EhRamoFederal(regraDistribuicao.Codigo))
        {
            erros.Add(new("regraAjuste", new DomainError(
                "ConfiguracaoDistribuicaoVagas.MotorDeAjusteVedadoNaLei12711",
                "Os motores de redução não se aplicam à distribuição pelo ramo federal (Lei 12.711) — a reconciliação dele é a do art. 11, parágrafo único.")));
        }

        if (codigosInformados.Distinct(StringComparer.Ordinal).Count() != codigosInformados.Count)
        {
            erros.Add(new("modalidades", new DomainError(
                "ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada",
                "Cada modalidade só pode ser selecionada uma vez por oferta.")));
        }

        // Coerência referencial: a Configuração só prova que os códigos de
        // origem/destino/par/fallback existem globalmente no cadastro — não
        // que participam desta oferta. Uma modalidade cujo cruzamento aponta
        // para um código fora do conjunto selecionado deixaria o motor de
        // vagas/remanejamento futuro apontando para uma modalidade ausente.
        DomainError? erroReferenciaCruzada = ValidarReferenciasCruzadas(modalidades, codigosInformados);
        if (erroReferenciaCruzada is not null)
        {
            erros.Add(new("modalidades", erroReferenciaCruzada));
        }

        if (erros.Count > 0)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.ValidationFailure(erros);
        }

        bool ehRamoFederal = RegraDistribuicaoVagasCodigo.EhRamoFederal(regraDistribuicao.Codigo);
        bool ehQuadroFixo = RegraDistribuicaoVagasCodigo.EhQuadroFixo(regraDistribuicao.Codigo);

        if (ehRamoFederal)
        {
            // INV-5: referência demográfica completa quando a regra é do ramo federal.
            if (referenciaDemografica is null)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                    "ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaObrigatoria",
                    "A distribuição pelo ramo federal (Lei 12.711) exige a referência de reserva demográfica (INV-5)."));
            }

            // INV-6: as 8 modalidades federais + AC são obrigatórias.
            IReadOnlyList<string> faltantes = [.. ModalidadesFederaisLei12711.CodigosComAc
                .Where(codigo => !codigosInformados.Contains(codigo, StringComparer.Ordinal))];
            if (faltantes.Count > 0)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                    "ConfiguracaoDistribuicaoVagas.ModalidadesFederaisIncompletas",
                    $"A distribuição pela Lei 12.711 exige as 8 modalidades federais e AC; faltam: {string.Join(", ", faltantes)} (INV-6)."));
            }

            // A fórmula do ramo federal (issue #848 §3.3.5) retira sempre da ampla
            // concorrência: AC = VO − Σ(sub-reservas) − Σ(retiradas). O domínio permite
            // ComposicaoOrigemCodigo apontar para qualquer modalidade selecionada (para
            // servir também o ramo institucional/remanejamento), mas uma retirada
            // federal com origem diferente de AC descontaria de AC no cálculo sem
            // nunca reduzir a modalidade que ela alega retirar.
            ModalidadeSelecionada? retiradaForaDeAc = modalidades.FirstOrDefault(
                m => m.ComposicaoVagas == ComposicaoVagasModalidade.RetiraDe
                    && !string.Equals(m.ComposicaoOrigemCodigo, ModalidadesFederaisLei12711.Ac, StringComparison.Ordinal));
            if (retiradaForaDeAc is not null)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                    "ConfiguracaoDistribuicaoVagas.RetiradaFederalDeveSerDeAmplaConcorrencia",
                    $"No ramo federal, a modalidade {retiradaForaDeAc.Codigo} (RETIRA_DE) só pode retirar da ampla concorrência (AC) — a fórmula do art. 10 desconta toda retirada de AC."));
            }
        }
        else if (referenciaDemografica is not null)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaIndevida",
                "A referência de reserva demográfica só se aplica à distribuição pelo ramo federal (Lei 12.711)."));
        }

        if (ehRamoFederal && regraAjuste is null)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.RegraAjusteObrigatoria",
                "A distribuição pelo ramo federal (Lei 12.711) exige a regra de ajuste (RECONCILIACAO-VAGAS-ART11-PU)."));
        }

        Result<QuadroMontado> quadro;
        if (ehRamoFederal || ehQuadroFixo)
        {
            DomainError? erroFronteira = ValidarFronteiraQuantidadeDeclarada(modalidades, ehRamoFederal);
            if (erroFronteira is not null)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(erroFronteira);
            }

            quadro = ehRamoFederal
                ? MontarQuadroFederal(voBase, pr, referenciaDemografica!, modalidades)
                : MontarQuadroInstitucional(voBase, modalidades, argsAjuste);
        }
        else
        {
            // Nem ramo federal nem quadro fixo: código de regra fora do vocabulário
            // reconhecido por esta story (ADR-0115). Preserva o comportamento anterior
            // a esta story — nenhum quadro é materializado, sem exigir RegraAjuste
            // nem completude de QuantidadeDeclarada. Em produção, regraDistribuicao é
            // sempre resolvida contra o catálogo (TipoRegra.RegraDistribuicaoVagas),
            // que só semeia os códigos de RegraDistribuicaoVagasCodigo.Todos — este ramo
            // só existe para não quebrar fixtures de outras stories que usam um código de
            // distribuição como mero preenchimento, sem exercitar o quadro de vagas.
            quadro = Result<QuadroMontado>.Success(new QuadroMontado([], VrNominal: 0, VrFinal: 0, Estouro: 0, CapadoEmVo: false, TotalPublicado: 0));
        }

        if (quadro.IsFailure)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(quadro.Error!);
        }

        QuadroMontado montado = quadro.Value!;

        ConfiguracaoDistribuicaoVagas configuracao = new()
        {
            OfertaCursoOrigemId = ofertaCursoOrigemId,
            VoBase = voBase,
            Pr = pr,
            RegraDistribuicao = regraDistribuicao,
            RegraAjuste = regraAjuste,
            ReferenciaDemografica = referenciaDemografica,
            VrNominal = montado.VrNominal,
            VrFinal = montado.VrFinal,
            Estouro = montado.Estouro,
            CapadoEmVo = montado.CapadoEmVo,
            TotalPublicado = montado.TotalPublicado,
        };

        foreach (ModalidadeSelecionada modalidade in modalidades)
        {
            modalidade.VincularConfiguracao(configuracao.Id);
            configuracao._modalidades.Add(modalidade);
        }

        foreach (VagaOfertada vaga in montado.Vagas)
        {
            vaga.VincularConfiguracao(configuracao.Id);
            configuracao._vagasOfertadas.Add(vaga);
        }

        return Result<ConfiguracaoDistribuicaoVagas>.Success(configuracao);
    }

    /// <summary>
    /// A fronteira entre o que é calculado e o que é declarado (issue #848
    /// §3.2): no ramo federal, as modalidades calculadas (sub-reserva
    /// <c>DENTRO_DO_VR</c> e a ampla concorrência <c>RESIDUAL_DO_VO</c>) nunca
    /// aceitam quantidade fixada, e as modalidades de retirada/suplemento
    /// sempre exigem; no ramo institucional, toda modalidade selecionada exige
    /// a quantidade fixada — não há cálculo algum.
    /// </summary>
    private static DomainError? ValidarFronteiraQuantidadeDeclarada(
        IReadOnlyList<ModalidadeSelecionada> modalidades, bool ehRamoFederal)
    {
        foreach (ModalidadeSelecionada modalidade in modalidades)
        {
            if (!ehRamoFederal)
            {
                if (modalidade.QuantidadeDeclarada is null)
                {
                    return new DomainError(
                        "ConfiguracaoDistribuicaoVagas.QuadroModalidadeAusente",
                        $"A modalidade {modalidade.Codigo} está selecionada mas não tem quantidade fixada pelo edital.");
                }

                continue;
            }

            bool calculada = modalidade.ComposicaoVagas is ComposicaoVagasModalidade.DentroDoVr or ComposicaoVagasModalidade.ResidualDoVo;
            bool exigeDeclaracao = modalidade.ComposicaoVagas is ComposicaoVagasModalidade.RetiraDe or ComposicaoVagasModalidade.SuplementarAoTotal;

            if (calculada && modalidade.QuantidadeDeclarada is not null)
            {
                return new DomainError(
                    "ConfiguracaoDistribuicaoVagas.QuantidadeCalculadaNaoInformavel",
                    $"A quantidade de {modalidade.Codigo} é calculada pela Lei 12.711 — não pode ser fixada pelo edital.");
            }

            if (exigeDeclaracao && modalidade.QuantidadeDeclarada is null)
            {
                return new DomainError(
                    "ConfiguracaoDistribuicaoVagas.QuantidadeDeclaradaObrigatoria",
                    $"A modalidade {modalidade.Codigo} (retirada ou suplemento) exige a quantidade fixada pelo edital.");
            }
        }

        return null;
    }

    private static Result<QuadroMontado> MontarQuadroFederal(
        int voBase,
        decimal pr,
        ReferenciaReservaDemograficaSnapshot demografica,
        IReadOnlyList<ModalidadeSelecionada> modalidades)
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal);
        Dictionary<string, int> suplementares = new(StringComparer.Ordinal);

        foreach (ModalidadeSelecionada modalidade in modalidades)
        {
            if (modalidade.ComposicaoVagas == ComposicaoVagasModalidade.RetiraDe)
            {
                retiradas[modalidade.Codigo] = modalidade.QuantidadeDeclarada!.Value;
            }
            else if (modalidade.ComposicaoVagas == ComposicaoVagasModalidade.SuplementarAoTotal)
            {
                suplementares[modalidade.Codigo] = modalidade.QuantidadeDeclarada!.Value;
            }
        }

        Result<QuadroVagasCalculado> calculo = CalculadoraQuadroVagasLei12711.Calcular(
            voBase, pr, demografica, retiradas, suplementares);
        if (calculo.IsFailure)
        {
            return Result<QuadroMontado>.Failure(calculo.Error!);
        }

        QuadroVagasCalculado calculado = calculo.Value!;
        List<VagaOfertada> vagas = [];

        foreach (ModalidadeSelecionada modalidade in modalidades)
        {
            if (!calculado.Quadro.TryGetValue(modalidade.Codigo, out int quantidade))
            {
                return Result<QuadroMontado>.Failure(new DomainError(
                    "ConfiguracaoDistribuicaoVagas.QuadroModalidadeAusente",
                    $"A modalidade {modalidade.Codigo} está selecionada mas não aparece no quadro calculado."));
            }

            Result<VagaOfertada> vaga = VagaOfertada.Criar(modalidade.ModalidadeOrigemId, modalidade.Codigo, quantidade);
            if (vaga.IsFailure)
            {
                return Result<QuadroMontado>.Failure(vaga.Error!);
            }

            vagas.Add(vaga.Value!);
        }

        return Result<QuadroMontado>.Success(new QuadroMontado(
            vagas, calculado.VrNominal, calculado.VrFinal, calculado.Estouro, calculado.CapadoEmVo, calculado.TotalPublicado));
    }

    /// <remarks>
    /// As quantidades <b>dividem</b> o total da oferta; não o ampliam nem o deixam
    /// incompleto. Nenhuma composição fica de fora da soma: neste ramo não há conjunto
    /// calculado ao lado ao qual uma suplementar pudesse acrescer — o total publicado é a
    /// soma do quadro, e dispensar a suplementar dele aceitaria um certame inteiro de
    /// suplementares como se distribuísse zero.
    /// </remarks>
    private static Result<QuadroMontado> MontarQuadroInstitucional(
        int voBase,
        IReadOnlyList<ModalidadeSelecionada> modalidades,
        ArgsRegraAjusteDistribuicao? argsAjuste)
    {
        string[] codigos = [.. modalidades.Select(static m => m.Codigo)];
        int[] quantidades = [.. modalidades.Select(static m => m.QuantidadeDeclarada!.Value)];
        int totalDeclarado = quantidades.Sum();

        // Faltar não tem motor: acrescer seria o sistema criar vaga que ninguém autorizou, e a
        // quantidade de vagas do certame é ato administrativo.
        if (totalDeclarado < voBase)
        {
            return Result<QuadroMontado>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.QuadroNaoCompletaVoBase",
                $"As quantidades do quadro somam {totalDeclarado}, e a oferta tem {voBase} vagas a distribuir."));
        }

        int excesso = totalDeclarado - voBase;
        if (excesso > 0 && argsAjuste is null)
        {
            return Result<QuadroMontado>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.QuadroExcedeVoBase",
                $"As quantidades do quadro somam {totalDeclarado}, acima das {voBase} vagas da oferta."));
        }

        Result<ReconciliadorQuadroInstitucional.QuadroReconciliado> reconciliado =
            ReconciliadorQuadroInstitucional.Reduzir(codigos, quantidades, excesso, argsAjuste ?? SemAjuste);
        if (reconciliado.IsFailure)
        {
            return Result<QuadroMontado>.Failure(reconciliado.Error!);
        }

        List<VagaOfertada> vagas = new(modalidades.Count);
        for (int i = 0; i < modalidades.Count; i++)
        {
            Result<VagaOfertada> vaga = VagaOfertada.Criar(
                modalidades[i].ModalidadeOrigemId, modalidades[i].Codigo, reconciliado.Value!.Quantidades[i]);
            if (vaga.IsFailure)
            {
                return Result<QuadroMontado>.Failure(vaga.Error!);
            }

            vagas.Add(vaga.Value!);
        }

        // Estouro e CapadoEmVo dizem, no mesmo vocabulário que o quadro federal já publica,
        // que houve reconciliação e de quanto — o edital sai com o quadro ajustado, e quem o
        // configurou precisa ver que ele não é o que digitou.
        int reduzido = reconciliado.Value!.Reduzido;
        return Result<QuadroMontado>.Success(new QuadroMontado(
            vagas, VrNominal: 0, VrFinal: 0, Estouro: reduzido, CapadoEmVo: reduzido > 0, voBase));
    }

    /// <summary>Motor inerte: sem excesso a reduzir, o reconciliador devolve o quadro intacto.</summary>
    private static readonly ArgsReduzirDe SemAjuste = new(string.Empty);

    /// <summary>
    /// Estado intermediário privado entre montar o quadro (calculado ou
    /// fixado) e materializar as entidades <see cref="VagaOfertada"/> — nunca
    /// exposto fora desta factory.
    /// </summary>
    private sealed record QuadroMontado(
        IReadOnlyList<VagaOfertada> Vagas, int VrNominal, int VrFinal, int Estouro, bool CapadoEmVo, int TotalPublicado);

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Valida que todo código de cruzamento (origem de RETIRA_DE, destino de
    /// DESTINO_UNICO, par/fallback de CRUZADO) referencia uma modalidade
    /// presente neste MESMO conjunto selecionado — não apenas uma modalidade
    /// existente no cadastro global. Sem esta checagem, uma
    /// modalidade poderia apontar para um código nunca selecionado nesta
    /// oferta, deixando o motor de vagas/remanejamento futuro sem a
    /// modalidade referenciada.
    /// </summary>
    /// <summary>Token de contrato da ação que reclassifica na ampla concorrência.</summary>
    private const string AcaoQuandoIndeferidoReclassificarAc = "RECLASSIFICAR_AC";

    /// <summary>Código da ampla concorrência no cadastro de modalidades.</summary>
    private const string AmplaConcorrenciaCodigo = "AC";

    /// <summary>
    /// Referências que uma modalidade faz a outra pelo código, e que só valem se a citada
    /// estiver no mesmo conjunto ofertado.
    /// </summary>
    private const string CodeComposicaoOrigemNaoSelecionada = "ConfiguracaoDistribuicaoVagas.ComposicaoOrigemNaoSelecionada";
    private const string CodeRemanejamentoDestinoNaoSelecionado = "ConfiguracaoDistribuicaoVagas.RemanejamentoDestinoNaoSelecionado";
    private const string CodeRemanejamentoParNaoSelecionado = "ConfiguracaoDistribuicaoVagas.RemanejamentoParNaoSelecionado";
    private const string CodeRemanejamentoFallbackNaoSelecionado = "ConfiguracaoDistribuicaoVagas.RemanejamentoFallbackNaoSelecionado";

    private static readonly (Func<ModalidadeSelecionada, string?> Ler, string Code, string Rotulo, string Concordancia)[] ReferenciasAoConjunto =
    [
        (static m => m.ComposicaoOrigemCodigo, CodeComposicaoOrigemNaoSelecionada, "a origem", "selecionada"),
        (static m => m.RemanejamentoDestino, CodeRemanejamentoDestinoNaoSelecionado, "o destino de remanejamento", "selecionado"),
        (static m => m.RemanejamentoPar, CodeRemanejamentoParNaoSelecionado, "o par de remanejamento", "selecionado"),
        (static m => m.RemanejamentoFallback, CodeRemanejamentoFallbackNaoSelecionado, "o fallback de remanejamento", "selecionado"),
    ];

    private static DomainError? ValidarReferenciasCruzadas(
        IReadOnlyList<ModalidadeSelecionada> modalidades, IReadOnlyList<string> codigosInformados) =>
        modalidades
            .Select(modalidade => PrimeiraViolacaoDe(modalidade, modalidades, codigosInformados))
            .FirstOrDefault(static violacao => violacao is not null);

    private static DomainError? PrimeiraViolacaoDe(
        ModalidadeSelecionada modalidade,
        IReadOnlyList<ModalidadeSelecionada> modalidades,
        IReadOnlyList<string> codigosInformados)
    {
        foreach ((Func<ModalidadeSelecionada, string?> ler, string code, string rotulo, string concordancia) in ReferenciasAoConjunto)
        {
            if (ler(modalidade) is { } referencia && !codigosInformados.Contains(referencia, StringComparer.Ordinal))
            {
                return new DomainError(
                    code,
                    $"Modalidade {modalidade.Codigo} referencia {rotulo} {referencia}, que não está {concordancia} nesta oferta.");
            }
        }

        return ParCruzadoNaoReciproco(modalidade, modalidades)
            ?? ReclassificacaoSemAmplaOfertada(modalidade, codigosInformados);
    }

    /// <remarks>
    /// A reciprocidade é exigida aqui, e não no cadastro, porque a primeira modalidade de um
    /// par não teria a que apontar quando é criada.
    /// <para>O auto-laço satisfaria a comparação recíproca — a modalidade aponta para si e
    /// encontra a si —, e um par de um nó só não remaneja para lugar nenhum.</para>
    /// </remarks>
    private static DomainError? ParCruzadoNaoReciproco(
        ModalidadeSelecionada modalidade,
        IReadOnlyList<ModalidadeSelecionada> modalidades)
    {
        if (modalidade is not { RegraRemanejamento: RegraRemanejamentoModalidade.Cruzado, RemanejamentoPar: { } par })
        {
            return null;
        }

        if (string.Equals(par, modalidade.Codigo, StringComparison.Ordinal))
        {
            return new DomainError(
                "ConfiguracaoDistribuicaoVagas.ParCruzadoNaoReciproco",
                $"Modalidade {modalidade.Codigo} forma par cruzado consigo mesma.");
        }

        ModalidadeSelecionada? contraparte = modalidades
            .FirstOrDefault(m => string.Equals(m.Codigo, par, StringComparison.Ordinal));

        bool reciproco = contraparte is null
            || (contraparte.RegraRemanejamento == RegraRemanejamentoModalidade.Cruzado
                && string.Equals(contraparte.RemanejamentoPar, modalidade.Codigo, StringComparison.Ordinal));

        return reciproco
            ? null
            : new DomainError(
                "ConfiguracaoDistribuicaoVagas.ParCruzadoNaoReciproco",
                $"Modalidade {modalidade.Codigo} forma par cruzado com {par}, que não aponta de volta para ela.");
    }

    /// <remarks>
    /// Diferente das demais, o código de destino não é declarado pelo operador: está implícito
    /// no token. Sem a recusa, um certame que não oferta ampla concorrência congela no
    /// envelope — imutável — uma instrução de reclassificar para modalidade que ele não tem.
    /// </remarks>
    private static DomainError? ReclassificacaoSemAmplaOfertada(
        ModalidadeSelecionada modalidade,
        IReadOnlyList<string> codigosInformados) =>
        string.Equals(modalidade.AcaoQuandoIndeferido, AcaoQuandoIndeferidoReclassificarAc, StringComparison.Ordinal)
        && !codigosInformados.Contains(AmplaConcorrenciaCodigo, StringComparer.Ordinal)
            ? new DomainError(
                "ConfiguracaoDistribuicaoVagas.ReclassificacaoParaAmplaSemAmplaOfertada",
                $"Modalidade {modalidade.Codigo} reclassifica em {AmplaConcorrenciaCodigo}, que não está selecionada nesta oferta.")
            : null;
}
