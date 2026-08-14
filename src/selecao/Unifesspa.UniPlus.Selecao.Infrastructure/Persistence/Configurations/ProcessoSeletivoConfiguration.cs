namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;

public sealed class ProcessoSeletivoConfiguration : IEntityTypeConfiguration<ProcessoSeletivo>
{
    private const int ReferenciaTemporalFatosTipoMaxLength = 20;

    // Mesmas larguras das demais referências ao rol_de_regras no módulo — a identidade
    // (codigo, versao, hash) tem a mesma forma onde quer que uma dimensão aplique regra.
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<ProcessoSeletivo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Trio de cidade da Unidade administradora, all-or-nothing (issue #1114):
        // espelha no banco a invariante já provada por
        // UnidadeAdministradoraSnapshot.ValidarReferenciaCidade — mesmo padrão de
        // InstituicaoConfiguration/UnidadeConfiguration.
        builder.ToTable("processos_seletivos", t => t.HasCheckConstraint(
            "ck_processos_seletivos_unidade_administradora_cidade_completa",
            "(unidade_administradora_cidade_codigo_ibge IS NULL AND unidade_administradora_cidade_nome IS NULL AND unidade_administradora_cidade_uf IS NULL) "
            + "OR (unidade_administradora_cidade_codigo_ibge IS NOT NULL AND unidade_administradora_cidade_nome IS NOT NULL AND unidade_administradora_cidade_uf IS NOT NULL)"));
        builder.HasKey(p => p.Id);
        // Chave Guid v7 gerada no domínio (EntityBase): ValueGeneratedNever
        // força o EF a tratar a chave como fornecida pela aplicação. Sem isso,
        // ao reconfigurar o agregado tracked (substituir filhos com Guid v7 já
        // preenchido), o EF marcaria os filhos novos como Modified → UPDATE de
        // linhas nunca inseridas. Convenção do repo (ver UnidadeIdentificadorHistorico).
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Nome).HasMaxLength(300).IsRequired();
        builder.Ignore(p => p.TipoProcessoOrigemId);
        builder.OwnsOne(p => p.TipoProcesso, tipo =>
        {
            tipo.Property(x => x.OrigemId)
                .HasColumnName("tipo_processo_origem_id")
                .IsRequired()
                .HasComment("Id de origem do tipo de processo em Configuração, sem FK cross-schema; congelado na criação.");
            tipo.Property(x => x.Codigo).HasColumnName("tipo_processo_codigo").HasMaxLength(64).IsRequired();
            tipo.Property(x => x.Nome).HasColumnName("tipo_processo_nome").HasMaxLength(200).IsRequired();
        });
        builder.Navigation(p => p.TipoProcesso).IsRequired();
        builder.Ignore(p => p.Tipo);
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        // Story #851 §3.4: NOT NULL, exigido na criação — sem produção, migration direta.
        builder.Property(p => p.OrigemCandidatos).HasConversion<int>().IsRequired();

        // Story #559: título e termo de aceite do formulário de inscrição — nuláveis, ausência
        // = sem título/termo configurado. Maxlengths espelhados em
        // LimitesDoEnvelope.NomeDeCadastro/TermoDeAceite (o decoder do envelope reidrata com o
        // mesmo limite).
        builder.Property(p => p.FormularioTitulo).HasMaxLength(300)
            .HasComment("Título do formulário de inscrição apresentado ao candidato. Ausência = sem título configurado.");
        builder.Property(p => p.FormularioTermoAceiteTexto).HasMaxLength(4000)
            .HasComment("Texto do termo de aceite do formulário de inscrição. Ausência = sem termo configurado.");

        // Issue #849 (CA-04 da Feature #40): quem responde pelo certame — NOT NULL, exigido
        // na criação, imutável depois. Escalar de topo sem FK cross-schema (ADR-0061) + owned
        // type snapshot-copy, maxlengths espelhando UnidadeConfiguration.
        builder.Property(p => p.UnidadeAdministradoraOrigemId)
            .IsRequired()
            .HasComment("Id da Unidade administradora em Organização Institucional (ADR-0061, sem FK cross-schema) — congelado na criação, imutável.");

        builder.OwnsOne(p => p.UnidadeAdministradora, u =>
        {
            u.Property(x => x.Sigla).HasColumnName("unidade_administradora_sigla").HasMaxLength(50).IsRequired()
                .HasComment("Snapshot-copy da sigla da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.Slug).HasColumnName("unidade_administradora_slug").HasMaxLength(64).IsRequired()
                .HasComment("Snapshot-copy do slug da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.Nome).HasColumnName("unidade_administradora_nome").HasMaxLength(250).IsRequired()
                .HasComment("Snapshot-copy do nome da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.Tipo).HasColumnName("unidade_administradora_tipo").HasMaxLength(30).IsRequired()
                .HasComment("Snapshot-copy do tipo organizacional da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");

            // Cidade da Unidade administradora (issue #1114) — snapshot-copy opcional
            // all-or-nothing: nula para processos criados antes desta Story (sem
            // produção, não há backfill); não-nula para processos novos, que o
            // gate de CriarProcessoSeletivoCommandHandler (CA-02) já exige.
            u.Property(x => x.CidadeCodigoIbge)
                .HasColumnName("unidade_administradora_cidade_codigo_ibge")
                .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
                .IsFixedLength()
                .HasComment("Snapshot-copy do código IBGE da cidade da Unidade administradora no momento da criação — nulo para processos anteriores à issue #1114.");
            u.Property(x => x.CidadeNome)
                .HasColumnName("unidade_administradora_cidade_nome")
                .HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength)
                .HasComment("Snapshot-copy do nome de exibição da cidade da Unidade administradora no momento da criação.");
            u.Property(x => x.CidadeUf)
                .HasColumnName("unidade_administradora_cidade_uf")
                .HasMaxLength(ReferenciaCidadeGeo.UfLength)
                .IsFixedLength()
                .HasComment("Snapshot-copy da UF da cidade da Unidade administradora no momento da criação.");
        });
        builder.Navigation(p => p.UnidadeAdministradora).IsRequired();

        // Localidade que rege a contagem de prazos (UNI-REQ-0111) — declarada na criação,
        // por isso as três colunas são NOT NULL e não há check all-or-nothing: aqui não
        // existe o estado "sem localidade" que o trio da Unidade administradora admite.
        builder.OwnsOne(p => p.Localidade, l =>
        {
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
                .HasComment("Nome do município da localidade regente — cache de exibição, não entra em cálculo de prazo.");
            l.Property(x => x.Uf)
                .HasColumnName("localidade_uf")
                .HasMaxLength(ReferenciaCidadeGeo.UfLength)
                .IsFixedLength()
                .IsRequired()
                .HasComment("UF da localidade regente — cache de exibição; a UF que vale é a derivada do prefixo do código.");
        });
        builder.Navigation(p => p.Localidade).IsRequired();

        // Convenção de contagem dos prazos (UNI-REQ-0112) — opcional na configuração: só
        // vira obrigatória quando alguma contagem do certame distingue dia útil, e essa
        // condição é do agregado, não da tabela. Colunas anuláveis, sem check
        // all-or-nothing próprio: o owned type do EF já traz as três juntas ou nenhuma.
        builder.OwnsOne(p => p.AlgoritmoContagemPrazo, algoritmo =>
        {
            algoritmo.Property(x => x.Codigo)
                .HasColumnName("algoritmo_contagem_prazo_codigo")
                .HasMaxLength(RegraCodigoMaxLength)
                .HasComment("Código da entrada de algoritmo de contagem do rol_de_regras que o certame declarou.");
            algoritmo.Property(x => x.Versao)
                .HasColumnName("algoritmo_contagem_prazo_versao")
                .HasMaxLength(RegraVersaoMaxLength)
                .HasComment("Versão da entrada declarada — evolução da convenção é versão nova, nunca alteração da vigente.");
            algoritmo.Property(x => x.Hash)
                .HasColumnName("algoritmo_contagem_prazo_hash")
                .HasMaxLength(HashLength)
                .IsFixedLength()
                .HasComment("Hash da definição resolvida no rol_de_regras — é o que prova que a convenção aplicada não mudou depois.");
        });

        // Coleções filhas do agregado: entidades próprias com FK para a raiz
        // (nunca owned types).
        builder.HasMany(p => p.Etapas)
            .WithOne()
            .HasForeignKey(e => e.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.OfertaAtendimento)
            .WithOne()
            .HasForeignKey<OfertaAtendimentoEspecializado>(o => o.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.DistribuicaoVagas)
            .WithOne()
            .HasForeignKey(d => d.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.BonusRegional)
            .WithOne()
            .HasForeignKey<ConfiguracaoBonusRegional>(b => b.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Divulgação pública (UNI-REQ-0050, issue #563) — 1:1, mesmo padrão de BonusRegional
        // acima. Ausência = default minimizado, não escolha administrativa pendente.
        builder.HasOne(p => p.ConfiguracaoDivulgacao)
            .WithOne()
            .HasForeignKey<ConfiguracaoDivulgacao>(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Taxa de inscrição e isenção (issue #1112) — 1:1, mesmo padrão de BonusRegional/
        // Divulgacao acima. Diferente dos dois, ausência aqui NÃO é estado publicável (CA-01) —
        // é dimensão do agregado do mesmo jeito, só o significado semântico da ausência muda.
        builder.HasOne(p => p.ConfiguracaoTaxaInscricao)
            .WithOne()
            .HasForeignKey<ConfiguracaoTaxaInscricao>(t => t.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.CriteriosDesempate)
            .WithOne()
            .HasForeignKey(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Classificacao)
            .WithOne()
            .HasForeignKey<ConfiguracaoClassificacao>(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cronograma de fases (Story #851) — 1..*, mesmo padrão de Etapas/DistribuicaoVagas.
        builder.HasMany(p => p.CronogramaFases)
            .WithOne()
            .HasForeignKey(f => f.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Documentos exigidos (Story #554) — 0..*, mesmo padrão de Etapas/CronogramaFases.
        builder.HasMany(p => p.DocumentosExigidos)
            .WithOne()
            .HasForeignKey(d => d.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Árvore de satisfação (Story #920) — 0..*, coleção PLANA (todos os nós, não só
        // raízes; ver NoExigenciaConfiguration). Cascade: FK obrigatória, mesmo padrão de
        // DocumentosExigidos — Clear()+Add() no replace-all do agregado já prova orphan-delete.
        builder.HasMany(p => p.NosExigencia)
            .WithOne()
            .HasForeignKey(n => n.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Grafo de coleta de fatos (Story #926) — quais fatos o processo coleta, em que ordem e
        // sob qual pré-condição. Cascade pelo mesmo motivo dos documentos exigidos: a FK é
        // obrigatória e o agregado substitui a coleção por inteiro.
        builder.HasMany(p => p.FatosColetados)
            .WithOne()
            .HasForeignKey(f => f.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.FatosColetados)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Regras de derivação (Story #927) — mesma disciplina de FatosColetados: FK obrigatória,
        // cascade, substituição por inteiro pelo agregado.
        builder.HasMany(p => p.RegrasDerivacao)
            .WithOne()
            .HasForeignKey(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.RegrasDerivacao)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ReferenciaTemporalFatos (Story #554, PR #896) — VO 0..1 sem identidade própria,
        // owned inline em processos_seletivos (nunca entidade filha própria — ela não tem
        // Id nem ciclo de vida próprio, diferente das coleções acima).
        builder.OwnsOne(p => p.ReferenciaTemporalFatos, referencia =>
        {
            referencia.Property(r => r.Tipo)
                .HasColumnName("referencia_temporal_fatos_tipo")
                .HasConversion(ReferenciaTipoConverter)
                .HasMaxLength(ReferenciaTemporalFatosTipoMaxLength);
            referencia.Property(r => r.Data).HasColumnName("referencia_temporal_fatos_data");
            referencia.Property(r => r.FaseId).HasColumnName("referencia_temporal_fatos_fase_id");
        });

        // A sessão editorial (ADR-0110 D3) — 1:1, como as demais filhas singulares. Ela é
        // efêmera (apagada no fechamento e no descarte) e não é evidência forense: a
        // auditoria com peso jurídico vive na VersaoConfiguracao, que é append-only.
        builder.HasOne(p => p.Rascunho)
            .WithOne()
            .HasForeignKey<RascunhoRetificacao>(r => r.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

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

        builder.Navigation(p => p.NosExigencia)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // RaizesDeExigencia é projeção EM MEMÓRIA de NosExigencia (NoPaiId == null) — sem
        // isto, o EF a descobre por convenção como uma SEGUNDA coleção de NoExigencia
        // (mesmo tipo de retorno IEnumerable<NoExigencia>) e cria uma FK-sombra duplicada
        // (processo_seletivo_id1) para desambiguar.
        builder.Ignore(p => p.RaizesDeExigencia);
    }

    private static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<ReferenciaTipo, string?> ReferenciaTipoConverter =
        new(
            tipo => tipo == ReferenciaTipo.Nenhuma ? null : tipo.ToCodigo(),
            codigo => ReferenciaTipoCodigo.FromCodigo(codigo));
}
