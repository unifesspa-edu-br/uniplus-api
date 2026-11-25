namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ProcessoSeletivoConfiguration : IEntityTypeConfiguration<ProcessoSeletivo>
{
    /// <summary>
    /// Comprimento máximo do título do processo. É a ORIGEM do título que a divulgação pública
    /// congela — quem o copia referencia esta constante, porque uma cópia mais curta recusa um
    /// cadastro legítimo no ponto em que já não há quem responda.
    /// </summary>
    internal const int NomeMaxLength = 300;

    private const int ReferenciaTemporalFatosTipoMaxLength = 20;

    // Mesmas larguras das demais referências ao catálogo de regras no módulo — a identidade
    // (codigo, versao, hash) tem a mesma forma onde quer que uma dimensão aplique regra.

    public void Configure(EntityTypeBuilder<ProcessoSeletivo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        // Trio de cidade da Unidade administradora, all-or-nothing:
        // espelha no banco a invariante já provada por
        // UnidadeAdministradoraSnapshot.ValidarReferenciaCidade — mesmo padrão de
        // InstituicaoConfiguration/UnidadeConfiguration.
        builder.ToTable("processos_seletivos", t =>
        {
            t.HasComment("Processos seletivos e suas configurações declaradas para a seleção de candidatos.");
            t.HasCheckConstraint(
                "ck_processos_seletivos_unidade_administradora_cidade_completa",
                "(unidade_administradora_cidade_codigo_ibge IS NULL AND unidade_administradora_cidade_nome IS NULL AND unidade_administradora_cidade_uf IS NULL) "
                + "OR (unidade_administradora_cidade_codigo_ibge IS NOT NULL AND unidade_administradora_cidade_nome IS NOT NULL AND unidade_administradora_cidade_uf IS NOT NULL)");
            t.HasCheckConstraint(
                "ck_processos_seletivos_numero_de_opcoes_de_curso",
                "numero_de_opcoes_de_curso IS NULL OR numero_de_opcoes_de_curso IN (1, 2)");
            t.HasCheckConstraint(
                "ck_processos_seletivos_situacao",
                $"situacao IN ({SqlDosCodigosDeContrato.Lista<StatusProcesso>()})");
            t.HasCheckConstraint(
                "ck_processos_seletivos_origem_candidatos",
                $"origem_candidatos IN ({SqlDosCodigosDeContrato.Lista<OrigemCandidatos>()})");
        });
        builder.HasKey(p => p.Id);
        // Chave Guid v7 gerada no domínio (EntityBase): ValueGeneratedNever
        // força o EF a tratar a chave como fornecida pela aplicação. Sem isso,
        // ao reconfigurar o agregado tracked (substituir filhos com Guid v7 já
        // preenchido), o EF marcaria os filhos novos como Modified → UPDATE de
        // linhas nunca inseridas. Convenção do repo (ver UnidadeIdentificadorHistorico).
        builder.Property(p => p.Id).ValueGeneratedNever().HasComment("Identificador do processo seletivo.");

        builder.Property(p => p.Nome).HasMaxLength(NomeMaxLength).IsRequired().HasComment("Nome do processo seletivo informado no cadastro.");

        // Identificador legível: nulável até a publicação, que o exige. O índice
        // único não filtra os excluídos logicamente — o endereço público de um certame excluído
        // não pode ser herdado por outro —, só os ausentes.
        builder.Property(p => p.IdentificadorLegivel)
            .HasConversion(IdentificadorLegivelConverter)
            .HasMaxLength(FormatoKebab.ComprimentoMaximo)
            .HasComment("Identificador legível escolhido no cadastro; dele derivam o endereço público do certame e a chave no acervo. Ausência = ainda não declarado.");
        // Declaração de que o processo aplica ou não o Fator de Atenuação das Desigualdades de Acesso (FADA): nula = ainda não declarado,
        // o que bloqueia a publicação.
        builder.Property(p => p.AplicaBonusRegional)
            .HasColumnName("aplica_fada")
            .HasComment("Indica se o processo aplica o Fator de Atenuação das Desigualdades de Acesso (FADA); ausente quando ainda não declarado.");
        // Quantos cursos o candidato escolhe na inscrição: nulo = ainda não declarado, o que bloqueia
        // a publicação. O intervalo é invariante do domínio e fica espelhado no banco.
        builder.Property(p => p.NumeroDeOpcoesDeCurso)
            .HasComment("Quantos cursos o candidato escolhe na inscrição (1 ou 2). Nulo = ainda não declarado.");
        builder.HasIndex(p => p.IdentificadorLegivel)
            .IsUnique()
            .HasFilter("identificador_legivel IS NOT NULL")
            .HasDatabaseName("ux_processos_seletivos_identificador_legivel");
        builder.Ignore(p => p.TipoProcessoOrigemId);
        builder.OwnsOne(p => p.TipoProcesso, tipo =>
        {
            tipo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            tipo.Property(x => x.OrigemId)
                .HasColumnName("tipo_processo_origem_id")
                .IsRequired()
                .HasComment("Identificador do tipo de processo no cadastro de origem, copiado na criação.");
            tipo.Property(x => x.Codigo).HasColumnName("tipo_processo_codigo").HasMaxLength(64).IsRequired().HasComment("Código do tipo de processo copiado do cadastro na criação.");
            tipo.Property(x => x.Nome).HasColumnName("tipo_processo_nome").HasMaxLength(200).IsRequired().HasComment("Nome do tipo de processo copiado do cadastro na criação.");
        });
        builder.Navigation(p => p.TipoProcesso).IsRequired();
        builder.Property(p => p.Status).HasColumnName("situacao").HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired()
            .HasComment("Situação do processo seletivo.");
        // NOT NULL, exigido na criação.
        builder.Property(p => p.OrigemCandidatos)
            .HasMaxLength(TamanhoDoCodigoDeContrato.Maximo)
            .HasComment("Como os candidatos chegam ao processo.")
            .IsRequired();

        // Quem responde pelo certame — NOT NULL, exigido
        // na criação, imutável depois. Escalar de topo sem FK cross-schema (ADR-0061) + owned
        // type snapshot-copy, maxlengths espelhando UnidadeConfiguration.
        builder.Property(p => p.UnidadeAdministradoraOrigemId)
            .IsRequired()
            .HasComment("Identificador da unidade administradora no cadastro de origem, copiado na criação e mantido sem alteração.");

        builder.OwnsOne(p => p.UnidadeAdministradora, u =>
        {
            u.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            u.Property(x => x.Sigla).HasColumnName("unidade_administradora_sigla").HasMaxLength(50).IsRequired()
                .HasComment("Cópia da sigla da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.Codigo).HasColumnName("unidade_administradora_codigo").HasMaxLength(50).IsRequired()
                .HasComment("Cópia do código da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.IdentificadorNoEnderecoWeb).HasColumnName("unidade_administradora_identificador_no_endereco_web").HasMaxLength(64).IsRequired()
                .HasComment("Cópia do identificador no endereço web da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.Nome).HasColumnName("unidade_administradora_nome").HasMaxLength(250).IsRequired()
                .HasComment("Cópia do nome da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.Tipo).HasColumnName("unidade_administradora_tipo").HasMaxLength(30).IsRequired()
                .HasComment("Código do tipo da unidade administradora copiado na criação: REITORIA, PRO_REITORIA, CENTRO, INSTITUTO, FACULDADE, DEPARTAMENTO, COORDENACAO, DIRETORIA, DIVISAO, NUCLEO ou OUTRO.");

            // Cidade da Unidade administradora: cópia opcional, tudo ou nada. É obrigatória para
            // processo novo, conforme CriarProcessoSeletivoCommandHandler.
            u.Property(x => x.CidadeCodigoIbge)
                .HasColumnName("unidade_administradora_cidade_codigo_ibge")
                .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
                .IsFixedLength()
                .HasComment("Cópia do código IBGE da cidade da Unidade administradora no momento da criação, quando a unidade a informa.");
            u.Property(x => x.CidadeNome)
                .HasColumnName("unidade_administradora_cidade_nome")
                .HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength)
                .HasComment("Cópia do nome de exibição da cidade da Unidade administradora no momento da criação.");
            u.Property(x => x.CidadeUf)
                .HasColumnName("unidade_administradora_cidade_uf")
                .HasMaxLength(ReferenciaCidadeGeo.UfLength)
                .IsFixedLength()
                .HasComment("Cópia da UF da cidade da Unidade administradora no momento da criação.");
        });
        builder.Navigation(p => p.UnidadeAdministradora).IsRequired();

        // Localidade que rege a contagem de prazos (UNI-REQ-0111) — declarada na criação,
        // por isso as três colunas são NOT NULL e não há check all-or-nothing: aqui não
        // existe o estado "sem localidade" que o trio da Unidade administradora admite.
        builder.OwnsOne(p => p.Localidade, l =>
        {
            l.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            l.Property(x => x.CodigoIbge)
                .HasColumnName("localidade_codigo_ibge")
                .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
                .IsFixedLength()
                .IsRequired()
                .HasComment("Código IBGE do município cujo calendário rege a contagem dos prazos — o único valor normativo da localidade.");
            l.Property(x => x.Nome)
                .HasColumnName("localidade_nome")
                .HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength)
                .IsRequired()
                .HasComment("Nome do município da localidade regente — guardado para exibição, não entra em cálculo de prazo.");
            l.Property(x => x.Uf)
                .HasColumnName("localidade_uf")
                .HasMaxLength(ReferenciaCidadeGeo.UfLength)
                .IsFixedLength()
                .IsRequired()
                .HasComment("UF da localidade regente — guardado para exibição; a UF que vale é a derivada do prefixo do código.");
        });
        builder.Navigation(p => p.Localidade).IsRequired();

        // Convenção de contagem dos prazos (UNI-REQ-0112) — opcional na configuração: só
        // vira obrigatória quando alguma contagem do certame distingue dia útil, e essa
        // condição é do agregado, não da tabela. Colunas anuláveis, sem check
        // all-or-nothing próprio: o owned type do EF já traz as três juntas ou nenhuma.
        builder.OwnsOne(p => p.AlgoritmoContagemPrazo, algoritmo =>
        {
            algoritmo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            algoritmo.ConfigurarReferenciaRegra("algoritmo_contagem_prazo");

            // A forma das colunas é a mesma de toda referência de regra; o que é desta aqui é o
            // que cada uma significa para o certame.
            algoritmo.Property(x => x.Codigo)
                .HasComment("Código da entrada de algoritmo de contagem do catálogo de regras que o certame declarou.");
            algoritmo.Property(x => x.Versao)
                .HasComment("Versão da entrada declarada — evolução da convenção é versão nova, nunca alteração da vigente.");
            algoritmo.Property(x => x.Hash)
                .HasComment("Resumo da definição resolvida no catálogo de regras — é o que prova que a convenção aplicada não mudou depois.");
        });

        // Coleções de entidades filhas do agregado
        // (nunca owned types).
        builder.HasMany(p => p.Etapas)
            .WithOne()
            .HasForeignKey(e => e.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.AtendimentoEspecializado)
            .WithOne()
            .HasForeignKey<AtendimentoEspecializado>(o => o.ProcessoSeletivoId)
            .HasConstraintName("fk_atendimentos_especializados_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.DistribuicaoVagas)
            .WithOne()
            .HasForeignKey(d => d.ProcessoSeletivoId)
            .HasConstraintName("fk_configuracoes_de_vagas_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.BonusRegional)
            .WithOne()
            .HasForeignKey<ConfiguracaoBonusRegional>(b => b.ProcessoSeletivoId)
            .HasConstraintName("fk_configuracoes_do_fada_processo_seletivo_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Divulgação pública (UNI-REQ-0050) — 1:1, mesmo padrão de BonusRegional
        // acima. Ausência = default minimizado, não escolha administrativa pendente.
        builder.HasOne(p => p.ConfiguracaoDivulgacao)
            .WithOne()
            .HasForeignKey<ConfiguracaoDivulgacao>(c => c.ProcessoSeletivoId)
            .HasConstraintName("fk_configuracoes_divulgacao_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        // Taxa de inscrição e isenção — 1:1, mesmo padrão de BonusRegional/
        // Divulgacao acima. Diferente da divulgação, ausência aqui NÃO é estado publicável
        // (CA-01), como a ausência da declaração do bônus — é dimensão do agregado do mesmo jeito,
        // só o significado semântico da ausência muda.
        builder.HasOne(p => p.ConfiguracaoTaxaInscricao)
            .WithOne()
            .HasForeignKey<ConfiguracaoTaxaInscricao>(t => t.ProcessoSeletivoId)
            .HasConstraintName("fk_configuracoes_taxa_inscricao_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.CriteriosDesempate)
            .WithOne()
            .HasForeignKey(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Classificacao)
            .WithOne()
            .HasForeignKey<ConfiguracaoClassificacao>(c => c.ProcessoSeletivoId)
            .HasConstraintName("fk_configuracoes_classificacao_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        // Cronograma de fases — 1..*, mesmo padrão de Etapas/DistribuicaoVagas.
        builder.HasMany(p => p.CronogramaFases)
            .WithOne()
            .HasForeignKey(f => f.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Documentos exigidos — 0..*, mesmo padrão de Etapas/CronogramaFases.
        builder.HasMany(p => p.DocumentosExigidos)
            .WithOne()
            .HasForeignKey(d => d.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // A coleção inclui as regras sem grupo e os itens de todos os grupos.
        builder.HasMany(p => p.RegrasDeComprovacao)
            .WithOne()
            .HasForeignKey(n => n.ProcessoSeletivoId)
            .HasConstraintName("fk_regras_de_comprovacao_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        // Grafo de coleta de fatos — quais fatos o processo coleta, em que ordem e
        // sob qual pré-condição. Cascade pelo mesmo motivo dos documentos exigidos: a FK é
        // obrigatória e o agregado substitui a coleção por inteiro.
        // Todo campo pertence ao processo — o item e o campo de grupo repetível —; os itens são a
        // projeção dos campos sem grupo.
        builder.HasMany(p => p.Campos)
            .WithOne()
            .HasForeignKey(f => f.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Campos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.FatosColetados);

        // Grupos repetíveis (UNI-REQ-0146) — mesma disciplina dos campos; os campos de cada grupo são
        // também campos do processo.
        builder.HasMany(p => p.GruposColetados)
            .WithOne()
            .HasForeignKey(g => g.ProcessoSeletivoId)
            .HasConstraintName("fk_grupos_dos_formularios_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.GruposColetados)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Formulários por finalidade (UNI-REQ-0144) — mesma disciplina de FatosColetados.
        builder.HasMany(p => p.Formularios)
            .WithOne()
            .HasForeignKey(f => f.ProcessoSeletivoId)
            .HasConstraintName("fk_formularios_processo_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Formularios)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Termos exigidos pelo formulário (UNI-REQ-0086) — mesma disciplina de FatosColetados.
        builder.HasMany(p => p.TermosExigidos)
            .WithOne()
            .HasForeignKey(t => t.ProcessoSeletivoId)
            .HasConstraintName("fk_termos_exigidos_formulario_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.TermosExigidos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Opções declaradas pelo processo — mesma disciplina de FatosColetados.
        builder.HasMany(p => p.OpcoesDeclaradas)
            .WithOne()
            .HasForeignKey(o => o.ProcessoSeletivoId)
            .HasConstraintName("fk_opcoes_declaradas_do_dado_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.OpcoesDeclaradas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Regras de derivação — mesma disciplina de FatosColetados: FK obrigatória,
        // cascade, substituição por inteiro pelo agregado.
        builder.HasMany(p => p.RegrasDerivacao)
            .WithOne()
            .HasForeignKey(c => c.ProcessoSeletivoId)
            .HasConstraintName("fk_calculos_dos_dados_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.RegrasDerivacao)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ReferenciaTemporalFatos — VO 0..1 sem identidade própria,
        // owned inline em processos_seletivos (nunca entidade filha própria — ela não tem
        // Id nem ciclo de vida próprio, diferente das coleções acima).
        builder.OwnsOne(p => p.ProvaPresencial, prova => prova.ConfigurarProvaPresencial());
        builder.OwnsOne(p => p.ReferenciaTemporalFatos, referencia =>
        {
            referencia.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            referencia.Property(r => r.Tipo)
                .HasColumnName("referencia_temporal_dados_tipo")
                .HasMaxLength(ReferenciaTemporalFatosTipoMaxLength)
                .HasComment("Referência para apurar os dados do candidato. Ausente quando não declarada.");
            referencia.Property(r => r.Data).HasColumnName("referencia_temporal_dados_data").HasComment("Data escolhida quando a referência para apurar os dados do candidato é uma data específica.");
            referencia.Property(r => r.FaseId).HasColumnName("referencia_temporal_dados_fase_id").HasComment("Identificador da fase escolhida quando a referência para apurar os dados do candidato é o início ou o fim de uma fase.");
        });

        builder.Navigation(p => p.Etapas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.DistribuicaoVagas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.CriteriosDesempate)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.CronogramaFases)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.DocumentosExigidos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.RegrasDeComprovacao)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // RegrasDeComprovacaoSemGrupo é projeção EM MEMÓRIA de RegrasDeComprovacao (RegraQueContemId == null) — sem
        // isto, o EF a descobre por convenção como uma SEGUNDA coleção de RegraDeComprovacao
        // (mesmo tipo de retorno IEnumerable<RegraDeComprovacao>) e cria uma FK-sombra duplicada
        // (processo_seletivo_id1) para desambiguar.
        builder.Ignore(p => p.RegrasDeComprovacaoSemGrupo);
    }

    // Falha ao ler um valor inválido do banco para preservar a integridade do identificador legível.
    private static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<IdentificadorLegivel, string> IdentificadorLegivelConverter =
        new(
            identificador => identificador.Valor,
            valor => ReidratarIdentificadorLegivel(valor));

    private static IdentificadorLegivel ReidratarIdentificadorLegivel(string valor)
    {
        Result<IdentificadorLegivel> resultado = IdentificadorLegivel.Criar(valor);
        return resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar o identificador legível '{valor}' ({resultado.Error!.Code}).");
    }

}
